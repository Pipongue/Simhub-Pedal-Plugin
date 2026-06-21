using System;
using System.IO;
using System.Reflection;

namespace PneumaticCalibratorSimHub
{
    /// <summary>
    /// Permet de réassigner quelle fonction (Frein à main/Accélérateur/Frein/Embrayage) est
    /// affichée/calibrée pour chaque canal physique (A0-A3), pour les utilisateurs qui ont câblé
    /// leurs capteurs différemment de l'assignation par défaut documentée dans l'onglet Flash.
    /// Ne change que l'étiquette affichée et sur quel canal portent les boutons de calibration :
    /// l'axe HID réellement piloté par chaque pin reste celui défini dans le firmware.
    /// </summary>
    public static class AxisAssignment
    {
        public static bool UseDefault { get; private set; } = true;
        public static readonly int[] FunctionForChannel = { 0, 1, 2, 3 };

        public static event Action Changed;

        private static string SettingsPath
        {
            get
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(dir ?? "", "PneumaticCalibrator.axisassignment");
            }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                string text = File.ReadAllText(SettingsPath).Trim();
                if (string.IsNullOrEmpty(text) || text.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    UseDefault = true;
                    return;
                }

                var parts = text.Split(',');
                if (parts.Length != 4) return;
                var values = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    if (!int.TryParse(parts[i], out values[i]) || values[i] < 0 || values[i] > 3) return;
                }
                UseDefault = false;
                Array.Copy(values, FunctionForChannel, 4);
            }
            catch { }
        }

        public static void SetUseDefault(bool useDefault)
        {
            UseDefault = useDefault;
            if (useDefault)
            {
                for (int i = 0; i < 4; i++) FunctionForChannel[i] = i;
            }
            Save();
            Changed?.Invoke();
        }

        public static void SetFunction(int channel, int functionIndex)
        {
            FunctionForChannel[channel] = functionIndex;
            Save();
            Changed?.Invoke();
        }

        private static void Save()
        {
            try
            {
                string content = UseDefault ? "default" : string.Join(",", FunctionForChannel);
                File.WriteAllText(SettingsPath, content);
            }
            catch { }
        }

        public static string GetFunctionNameKey(int channel) =>
            PedalSerial.ChannelNameKeys[UseDefault ? channel : FunctionForChannel[channel]];

        public static string GetFunctionName(int channel) => Localization.T(GetFunctionNameKey(channel));
    }
}
