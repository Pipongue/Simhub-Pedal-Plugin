using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PneumaticCalibratorSimHub
{
    /// <summary>
    /// Vérifie/installe les mises à jour du plugin via les Releases GitHub.
    /// Le DLL ne peut pas être remplacé tant que SimHub l'a chargé (verrou de fichier) :
    /// la mise à jour est donc téléchargée sous un nom temporaire, puis substituée par un
    /// petit script PowerShell détaché qui attend la fermeture de SimHub avant de l'appliquer.
    /// </summary>
    public static class PluginUpdater
    {
        private const string GitHubOwnerRepo = "Pipongue/Simhub-Pedal-Plugin";

        public class UpdateInfo
        {
            public string Version;
            public string DownloadUrl;
        }

        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version;

        public static async Task<UpdateInfo> CheckForUpdateAsync()
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("PneumaticCalibratorSimHub-Updater");
                // On liste TOUTES les releases (pas /releases/latest, qui exclut les pre-releases
                // par défaut côté API GitHub) et on prend la version la plus haute manuellement.
                string url = $"https://api.github.com/repos/{GitHubOwnerRepo}/releases";
                string json = await http.GetStringAsync(url).ConfigureAwait(false);

                var serializer = new JavaScriptSerializer();
                var releases = serializer.Deserialize<object[]>(json);
                if (releases == null) return null;

                Version bestVersion = null;
                string bestVersionStr = null;
                string bestDownloadUrl = null;

                foreach (var releaseObj in releases)
                {
                    if (!(releaseObj is Dictionary<string, object> release)) continue;
                    if (release.TryGetValue("draft", out var draftObj) && draftObj is bool draft && draft) continue;

                    string tag = release.TryGetValue("tag_name", out var tagObj) ? tagObj as string : null;
                    if (string.IsNullOrEmpty(tag)) continue;
                    string versionStr = tag.TrimStart('v', 'V');
                    if (!Version.TryParse(versionStr, out var remoteVersion)) continue;
                    if (remoteVersion <= CurrentVersion) continue;
                    if (bestVersion != null && remoteVersion <= bestVersion) continue;

                    string downloadUrl = null;
                    if (release.TryGetValue("assets", out var assetsObj) && assetsObj is object[] assets)
                    {
                        foreach (var assetObj in assets)
                        {
                            if (!(assetObj is Dictionary<string, object> asset)) continue;
                            string name = asset.TryGetValue("name", out var n) ? n as string : null;
                            if (name != null && name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.TryGetValue("browser_download_url", out var u) ? u as string : null;
                                break;
                            }
                        }
                    }
                    if (downloadUrl == null) continue;

                    bestVersion = remoteVersion;
                    bestVersionStr = versionStr;
                    bestDownloadUrl = downloadUrl;
                }

                if (bestVersion == null) return null;
                return new UpdateInfo { Version = bestVersionStr, DownloadUrl = bestDownloadUrl };
            }
        }

        /// <summary>
        /// Télécharge la mise à jour et programme son installation à la prochaine fermeture
        /// de SimHub (un script détaché attend que le process libère le fichier).
        /// </summary>
        public static async Task DownloadAndScheduleInstallAsync(UpdateInfo update, Action<string> log)
        {
            string asmPath = Assembly.GetExecutingAssembly().Location;
            string dir = Path.GetDirectoryName(asmPath);
            string fileName = Path.GetFileName(asmPath);
            string stagedPath = Path.Combine(dir, fileName + ".update");

            log?.Invoke($"Téléchargement de la version {update.Version}...");
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("PneumaticCalibratorSimHub-Updater");
                var bytes = await http.GetByteArrayAsync(update.DownloadUrl).ConfigureAwait(false);
                File.WriteAllBytes(stagedPath, bytes);
            }
            log?.Invoke("Téléchargement terminé.");

            ScheduleSwapOnSimHubExit(dir, fileName, stagedPath);
            log?.Invoke("Mise à jour programmée : elle s'appliquera à la prochaine fermeture de SimHub.");
        }

        private static void ScheduleSwapOnSimHubExit(string dir, string fileName, string stagedPath)
        {
            int simHubPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            string targetPath = Path.Combine(dir, fileName);

            // Script PowerShell détaché : attend que ce process SimHub se termine, puis
            // remplace l'ancien DLL par la version téléchargée.
            string script =
                $"Wait-Process -Id {simHubPid} -ErrorAction SilentlyContinue; " +
                $"Start-Sleep -Seconds 1; " +
                $"Copy-Item -Path '{stagedPath}' -Destination '{targetPath}' -Force; " +
                $"Remove-Item -Path '{stagedPath}' -Force -ErrorAction SilentlyContinue;";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(psi);
        }
    }
}
