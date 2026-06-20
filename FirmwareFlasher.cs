using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PneumaticCalibratorSimHub
{
    /// <summary>
    /// Flashe le firmware pneumatic_hid sur la pédale via avrdude, en pilotant le reset
    /// 1200-bauds standard du bootloader Caterina (Arduino Leonardo/Pro Micro). Les outils
    /// (avrdude + .hex) sont embarqués en ressources et extraits dans un dossier temporaire.
    /// </summary>
    public static class FirmwareFlasher
    {
        public static async Task FlashAsync(string targetPort, Action<string> log)
        {
            if (string.IsNullOrEmpty(targetPort))
                throw new InvalidOperationException("Aucun port Arduino sélectionné.");

            log($"Port cible : {targetPort}");
            log("Déclenchement du bootloader (1200-baud touch)...");

            var portsBefore = SerialPort.GetPortNames();
            log($"Ports avant reset : {string.Join(", ", portsBefore)}");

            using (var touch = new SerialPort(targetPort, 1200))
            {
                touch.DtrEnable = true;
                touch.Open();
                await Task.Delay(200);
                touch.DtrEnable = false;
                touch.Close();
            }

            string bootPort = await WaitForBootloaderPortAsync(targetPort, portsBefore, TimeSpan.FromSeconds(12), log);
            if (bootPort == null)
                throw new TimeoutException("Le port bootloader n'est pas apparu (timeout). Vérifie que la pédale est bien branchée.");
            log($"Port bootloader détecté : {bootPort}");

            string tmpDir = Path.Combine(Path.GetTempPath(), "PneumaticCalibratorSimHub_flash");
            Directory.CreateDirectory(tmpDir);
            string avrdudeExe  = ExtractResource("Tools.avrdude.avrdude.exe",  tmpDir, "avrdude.exe");
            string avrdudeConf = ExtractResource("Tools.avrdude.avrdude.conf", tmpDir, "avrdude.conf");
            string hexPath     = ExtractResource("Firmware.pneumatic_hid.hex", tmpDir, "pneumatic_hid.hex");

            log("Flash en cours via avrdude...");
            string args = $"-C \"{avrdudeConf}\" -v -p atmega32u4 -c avr109 -P {bootPort} -b 57600 -D -U flash:w:\"{hexPath}\":i";
            int exitCode = await RunProcessAsync(avrdudeExe, args, log);
            if (exitCode != 0)
                throw new Exception($"avrdude a retourné le code {exitCode}.");

            log("Flash terminé.");
        }

        // Boucle unique : ne suppose pas un ordre strict disparition→réapparition,
        // car certains drivers USB ne font jamais "disparaître" le port (ou trop vite pour être vu).
        private static async Task<string> WaitForBootloaderPortAsync(
            string originalPort, string[] portsBefore, TimeSpan timeout, Action<string> log)
        {
            var deadline = DateTime.UtcNow + timeout;
            var beforeSet = new System.Collections.Generic.HashSet<string>(portsBefore, StringComparer.OrdinalIgnoreCase);
            bool sawDisappear = false;
            string[] lastSeen = portsBefore;

            while (DateTime.UtcNow < deadline)
            {
                var current = SerialPort.GetPortNames();
                if (!current.SequenceEqual(lastSeen, StringComparer.OrdinalIgnoreCase))
                {
                    log($"Ports : {string.Join(", ", current)}");
                    lastSeen = current;
                }

                bool originalPresent = current.Contains(originalPort, StringComparer.OrdinalIgnoreCase);
                if (!originalPresent) sawDisappear = true;

                string newPort = current.FirstOrDefault(p => !beforeSet.Contains(p));
                if (newPort != null) return newPort;

                if (sawDisappear && originalPresent) return originalPort;

                await Task.Delay(200);
            }

            var finalPorts = SerialPort.GetPortNames();
            if (finalPorts.Contains(originalPort, StringComparer.OrdinalIgnoreCase))
            {
                log("Le port n'a jamais semblé changer — tentative quand même sur le port d'origine.");
                return originalPort;
            }
            return null;
        }

        private static string ExtractResource(string resourceSuffix, string destDir, string fileName)
        {
            var asm = Assembly.GetExecutingAssembly();
            string resName = $"PneumaticCalibratorSimHub.{resourceSuffix}";
            using (var stream = asm.GetManifestResourceStream(resName))
            {
                if (stream == null)
                    throw new FileNotFoundException($"Ressource embarquée introuvable : {resName}");
                string destPath = Path.Combine(destDir, fileName);
                using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                    stream.CopyTo(fs);
                return destPath;
            }
        }

        private static Task<int> RunProcessAsync(string exePath, string args, Action<string> onOutput)
        {
            var tcs = new TaskCompletionSource<int>();
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) onOutput(e.Data); };
            proc.ErrorDataReceived  += (s, e) => { if (e.Data != null) onOutput(e.Data); };
            proc.Exited += (s, e) => { tcs.TrySetResult(proc.ExitCode); proc.Dispose(); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            return tcs.Task;
        }
    }
}
