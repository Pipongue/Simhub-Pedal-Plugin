using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;

namespace PneumaticCalibratorSimHub
{
    /// <summary>
    /// Communique avec le firmware pneumatic_hid (4 canaux : A0=Frein à main, A1=Accélérateur,
    /// A2=Frein, A3=Embrayage). Connexion à la demande uniquement (pas de port ouvert en permanence) :
    /// utilisée seulement pendant que le panneau de calibration SimHub est ouvert.
    /// </summary>
    public class PedalSerial : IDisposable
    {
        public const int NumChannels = 4;
        public static readonly string[] ChannelNames = { "Frein à main", "Accélérateur", "Frein", "Embrayage" };
        public static readonly string[] ChannelPins  = { "A0", "A1", "A2", "A3" };

        public event Action<string> Log;
        public event Action<int, int, int> RawOutUpdated;   // channel, raw, output
        public event Action<int, string, int> ConfigUpdated; // channel, kind(MIN/MAX/DZN/DZX), value
        public event Action<int, bool> ChannelConnectivityChanged; // channel, connected
        public event Action Disconnected;

        private SerialPort _port;
        private Thread _readThread;
        private volatile bool _running;

        public bool IsConnected => _port != null && _port.IsOpen;

        // VID/PID connus des cartes compatibles Caterina (Leonardo/Pro Micro), peu importe le nom
        // affiché par Windows (qui peut être personnalisé/renommé et donc non fiable).
        // VID_2341 = Arduino, VID_1B4F = SparkFun. PID 8036 = sketch, 0036 = bootloader.
        private static readonly (string Vid, string Pid)[] KnownVidPid =
        {
            ("2341", "8036"), ("2341", "0036"), // Arduino Leonardo
            ("1B4F", "9203"), ("1B4F", "9204"), // SparkFun Pro Micro (5V/3.3V)
            ("1B4F", "0036"),                    // SparkFun Pro Micro, bootloader
        };

        public static List<(string Port, string Label)> ListArduinoPorts()
        {
            var result = new List<(string Port, string Label)>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString() ?? "";
                        string pnpId = obj["PNPDeviceID"]?.ToString() ?? "";

                        bool nameMatch = name.IndexOf("Arduino", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Pro Micro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("SparkFun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Leonardo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Pneumatic", StringComparison.OrdinalIgnoreCase) >= 0;

                        // Détection par VID/PID : fonctionne même si le nom affiché a été
                        // personnalisé ou est complètement générique/farfelu.
                        bool vidPidMatch = false;
                        var vidM = Regex.Match(pnpId, @"VID_([0-9A-Fa-f]{4})");
                        var pidM = Regex.Match(pnpId, @"PID_([0-9A-Fa-f]{4})");
                        if (vidM.Success && pidM.Success)
                        {
                            foreach (var (vid, pid) in KnownVidPid)
                            {
                                if (string.Equals(vidM.Groups[1].Value, vid, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(pidM.Groups[1].Value, pid, StringComparison.OrdinalIgnoreCase))
                                {
                                    vidPidMatch = true;
                                    break;
                                }
                            }
                        }

                        if (nameMatch || vidPidMatch)
                        {
                            var m = Regex.Match(name, @"COM\d+");
                            if (m.Success) result.Add((m.Value, name));
                        }
                    }
                }
            }
            catch { }

            // Filet de sécurité : si rien n'a matché (nom et VID/PID inconnus, carte custom...),
            // on liste quand même tous les ports série disponibles pour permettre un choix manuel.
            if (result.Count == 0)
            {
                foreach (var port in SerialPort.GetPortNames())
                    result.Add((port, $"{port} (non identifié — vérifier manuellement)"));
            }

            return result;
        }

        public void Connect(string portName)
        {
            if (IsConnected) return;
            _port = new SerialPort(portName, 115200) { ReadTimeout = 200, WriteTimeout = 250, DtrEnable = true };
            _port.Open();
            _running = true;
            _readThread = new Thread(ReadLoop) { IsBackground = true };
            _readThread.Start();
            Send("GET");
        }

        public void Disconnect()
        {
            _running = false;
            var p = _port;
            _port = null;
            _readThread = null;
            if (p != null)
            {
                // Close() peut bloquer indéfiniment sur un port dont le périphérique a disparu
                // pendant qu'un ReadLine() est en cours -- on ferme sur un thread jetable.
                System.Threading.Tasks.Task.Run(() => { try { p.Close(); p.Dispose(); } catch { } });
            }
        }

        public void Send(string cmd)
        {
            var p = _port;
            if (p == null) return;
            try { p.WriteLine(cmd); }
            catch { NotifyDisconnected(); }
        }

        public void SetMin(int ch) => Send($"MIN{ch}");
        public void SetMax(int ch) => Send($"MAX{ch}");
        public void SetDeadzoneMin(int ch, int value) => Send($"DZN{ch}={value}");
        public void SetDeadzoneMax(int ch, int value) => Send($"DZX{ch}={value}");
        public void RequestAll() => Send("GET");

        private static readonly Regex DataRegex = new Regex(@"(RAW|OUT|CONN)(\d)=(-?\d+)");
        private static readonly Regex ConfigRegex = new Regex(@"^(MIN|MAX|DZN|DZX)(\d)=(-?\d+)$");

        private void ReadLoop()
        {
            var rawByCh = new int[NumChannels];
            var lastConnected = new bool?[NumChannels];
            while (_running && _port != null && _port.IsOpen)
            {
                try
                {
                    string line = _port.ReadLine().Trim();
                    if (line.Length == 0) continue;
                    Log?.Invoke(line);

                    if (line.StartsWith("RAW", StringComparison.Ordinal))
                    {
                        foreach (Match m in DataRegex.Matches(line))
                        {
                            int ch = int.Parse(m.Groups[2].Value);
                            if (ch < 0 || ch >= NumChannels) continue;
                            int val = int.Parse(m.Groups[3].Value);
                            switch (m.Groups[1].Value)
                            {
                                case "RAW": rawByCh[ch] = val; break;
                                case "OUT": RawOutUpdated?.Invoke(ch, rawByCh[ch], val); break;
                                case "CONN":
                                    bool conn = val != 0;
                                    if (lastConnected[ch] != conn)
                                    {
                                        lastConnected[ch] = conn;
                                        ChannelConnectivityChanged?.Invoke(ch, conn);
                                    }
                                    break;
                            }
                        }
                    }
                    else
                    {
                        var m = ConfigRegex.Match(line);
                        if (m.Success)
                        {
                            int ch = int.Parse(m.Groups[2].Value);
                            if (ch >= 0 && ch < NumChannels)
                                ConfigUpdated?.Invoke(ch, m.Groups[1].Value, int.Parse(m.Groups[3].Value));
                        }
                    }
                }
                catch (TimeoutException) { }
                catch
                {
                    _running = false;
                    NotifyDisconnected();
                    break;
                }
            }
        }

        private void NotifyDisconnected()
        {
            _running = false;
            Disconnected?.Invoke();
        }

        public void Dispose() => Disconnect();
    }
}
