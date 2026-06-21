using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PneumaticCalibratorSimHub
{
    public enum Lang { Fr, En }

    public static class Localization
    {
        public static Lang Current { get; private set; } = Lang.Fr;
        public static event Action LanguageChanged;

        private static string SettingsPath
        {
            get
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(dir ?? "", "PneumaticCalibrator.language");
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string text = File.ReadAllText(SettingsPath).Trim();
                    Current = text.Equals("en", StringComparison.OrdinalIgnoreCase) ? Lang.En : Lang.Fr;
                }
            }
            catch { }
        }

        public static void SetLanguage(Lang lang)
        {
            if (Current == lang) return;
            Current = lang;
            try { File.WriteAllText(SettingsPath, lang == Lang.En ? "en" : "fr"); } catch { }
            LanguageChanged?.Invoke();
        }

        public static string T(string key) => Strings.TryGetValue(key, out var pair) ? (Current == Lang.En ? pair.En : pair.Fr) : key;
        public static string T(string key, params object[] args) => string.Format(T(key), args);

        private static readonly Dictionary<string, (string Fr, string En)> Strings = new Dictionary<string, (string Fr, string En)>
        {
            ["Tab.Calibration"] = ("Calibration", "Calibration"),
            ["Tab.Flash"] = ("Flash", "Flash"),
            ["Tab.Settings"] = ("Réglages", "Settings"),

            ["Connect"] = ("Connecter", "Connect"),
            ["Disconnect"] = ("Déconnecter", "Disconnect"),
            ["Status.Disconnected"] = ("● Déconnecté", "● Disconnected"),
            ["Status.Connected"] = ("● Connecté   {0}", "● Connected   {0}"),
            ["Status.Error"] = ("● Erreur : {0}", "● Error: {0}"),
            ["Status.NotFound"] = ("● Arduino introuvable", "● Arduino not found"),
            ["NoDeviceFound"] = ("Aucun appareil détecté", "No device found"),
            ["Calibration.NoDeviceHint"] = ("Si votre Arduino n'apparaît pas dans la liste, il doit d'abord être flashé avec le firmware de ce plugin (onglet Flash). La calibration ne fonctionne qu'avec ce firmware, pas avec un firmware tiers.",
                                              "If your Arduino doesn't appear in the list, it must first be flashed with this plugin's firmware (Flash tab). Calibration only works with this firmware, not with third-party firmware."),

            ["Channel.Handbrake"] = ("Frein à main", "Handbrake"),
            ["Channel.Throttle"] = ("Accélérateur", "Throttle"),
            ["Channel.Brake"] = ("Frein", "Brake"),
            ["Channel.Clutch"] = ("Embrayage", "Clutch"),

            ["Raw"] = ("RAW", "RAW"),
            ["Output"] = ("OUTPUT", "OUTPUT"),
            ["SetMin"] = ("↓ Set MIN", "↓ Set MIN"),
            ["SetMax"] = ("↑ Set MAX", "↑ Set MAX"),
            ["Deadzone"] = ("Deadzone", "Deadzone"),

            ["Flash.Warning"] = ("⚠ Attention :", "⚠ Warning:"),
            ["Flash.WarningBody"] = (" flasher la carte efface définitivement tout programme actuellement installé sur l'Arduino. Cette opération est irréversible et le programme existant ne pourra pas être récupéré.",
                                      " flashing the board permanently erases any program currently installed on the Arduino. This operation is irreversible and the existing program cannot be recovered."),
            ["Flash.HardwareRequired"] = ("MATÉRIEL REQUIS", "REQUIRED HARDWARE"),
            ["Flash.BoardLine1"] = ("Carte : ", "Board: "),
            ["Flash.BoardBold"] = ("Arduino Pro Micro (ATmega32U4)", "Arduino Pro Micro (ATmega32U4)"),
            ["Flash.BoardLine1End"] = (" ou compatible avec bootloader Caterina (Leonardo, SparkFun Pro Micro).", " or compatible with the Caterina bootloader (Leonardo, SparkFun Pro Micro)."),
            ["Flash.BoardNote"] = ("La carte doit déjà avoir un bootloader Caterina installé (c'est le cas en sortie d'usine pour ces modèles). Le flash écrit uniquement le programme applicatif, pas le bootloader.",
                                    "The board must already have a Caterina bootloader installed (factory default for these models). Flashing only writes the application program, not the bootloader."),
            ["Flash.CompatibleSensors"] = ("CAPTEURS COMPATIBLES", "COMPATIBLE SENSORS"),
            ["Flash.SensorsIntro"] = ("Le firmware lit une tension analogique brute (0-5V) sur chaque pin : il fonctionne avec n'importe quel capteur fournissant directement ce type de signal, pas seulement des capteurs de pression pneumatique.",
                                       "The firmware reads a raw analog voltage (0-5V) on each pin: it works with any sensor that directly provides this type of signal, not just pneumatic pressure sensors."),
            ["Flash.Compatible"] = ("✓ Compatibles", "✓ Compatible"),
            ["Flash.CompatibleList"] = ("Capteurs de pression pneumatique • Potentiomètres linéaires ou rotatifs • Capteurs à effet Hall (linéaires ou rotatifs) à sortie analogique • Load cells avec module d'amplification intégré à sortie analogique ratiométrique (type 0.5-4.5V)",
                                         "Pneumatic pressure sensors • Linear or rotary potentiometers • Hall-effect sensors (linear or rotary) with analog output • Load cells with a built-in amplifier module and ratiometric analog output (0.5-4.5V type)"),
            ["Flash.Incompatible"] = ("✗ Non compatibles (sans modifier le firmware)", "✗ Not compatible (without modifying the firmware)"),
            ["Flash.IncompatibleList"] = ("Load cells brutes (pont de Wheatstone sans ampli, signal en mV) • Load cells avec module HX711 (protocole numérique, pas analogique) • Encodeurs rotatifs incrémentaux à quadrature (nécessitent un comptage d'impulsions par interruption)",
                                           "Raw load cells (Wheatstone bridge without amplifier, mV-level signal) • Load cells with an HX711 module (digital protocol, not analog) • Incremental quadrature rotary encoders (require interrupt-based pulse counting)"),
            ["Flash.Wiring"] = ("BRANCHEMENT DES CAPTEURS", "SENSOR WIRING"),
            ["Flash.WiringIntro"] = ("Chaque pédale (capteur de pression ou potentiomètre) se branche sur une pin analogique fixe :",
                                       "Each pedal (pressure sensor or potentiometer) connects to a fixed analog pin:"),
            ["Flash.WiringNote"] = ("Le signal du capteur va sur la broche analogique, l'alimentation et la masse sur 5V/GND. Une pin sans capteur branché est détectée automatiquement et sa colonne reste masquée dans l'onglet Calibration.",
                                      "The sensor signal goes to the analog pin, power and ground to 5V/GND. A pin with no sensor connected is detected automatically and its column stays hidden in the Calibration tab."),
            ["Flash.Procedure"] = ("PROCÉDURE", "PROCEDURE"),
            ["Flash.ProcedureSteps"] = ("1. Branche la carte en USB.   2. Sélectionne son port ci-dessous.   3. Clique \"Flasher firmware\" et ne débranche pas pendant l'opération (~15 secondes). La carte redémarre et se reconnecte automatiquement à la fin.",
                                          "1. Plug in the board via USB.   2. Select its port below.   3. Click \"Flash firmware\" and don't unplug during the operation (~15 seconds). The board restarts and reconnects automatically when done."),
            ["Flash.Button"] = ("⚡ Flasher firmware", "⚡ Flash firmware"),
            ["Flash.ButtonInProgress"] = ("FLASH EN COURS...", "FLASHING..."),

            ["Settings.DevOptions"] = ("OPTIONS DÉVELOPPEUR", "DEVELOPER OPTIONS"),
            ["Settings.ShowAllAxes"] = ("Afficher tous les axes (même débranchés, dans l'onglet Calibration)",
                                          "Show all axes (even disconnected, in the Calibration tab)"),
            ["Settings.ShowRawValue"] = ("Afficher la valeur brute du capteur", "Show raw sensor value"),
            ["Settings.AxisAssignment"] = ("ASSIGNATION DES AXES", "AXIS ASSIGNMENT"),
            ["Settings.DefaultAssignment"] = ("Assignation par défaut (A0 = Frein à main, A1 = Accélérateur, A2 = Frein, A3 = Embrayage)",
                                                "Default assignment (A0 = Handbrake, A1 = Throttle, A2 = Brake, A3 = Clutch)"),
            ["Settings.AssignmentHint"] = ("Si désactivé, un menu déroulant apparaît sur chaque canal dans l'onglet Calibration pour choisir manuellement quelle fonction (frein à main, accélérateur, frein, embrayage) est assignée à chaque capteur connecté.",
                                             "If disabled, a dropdown appears on each channel in the Calibration tab to manually choose which function (handbrake, throttle, brake, clutch) is assigned to each connected sensor."),
            ["Settings.Language"] = ("LANGUE", "LANGUAGE"),
            ["Settings.LangFr"] = ("Français", "Français"),
            ["Settings.LangEn"] = ("English", "English"),
            ["Settings.Update"] = ("MISE À JOUR", "UPDATE"),
            ["Settings.CurrentVersion"] = ("Version actuelle : {0}", "Current version: {0}"),
            ["Settings.CheckUpdate"] = ("Vérifier les mises à jour", "Check for updates"),
            ["Settings.Checking"] = ("Vérification en cours...", "Checking..."),
            ["Settings.UpToDate"] = ("Vous avez déjà la dernière version.", "You already have the latest version."),
            ["Settings.DevBuildStatus"] = ("Vous utilisez une version de développement (non publiée) : {0}.", "You are running a development version (not published): {0}."),
            ["Settings.NewVersion"] = ("Nouvelle version disponible : {0}", "New version available: {0}"),
            ["Settings.CheckFailed"] = ("Échec de la vérification : {0}", "Check failed: {0}"),
            ["Settings.DownloadInstall"] = ("Télécharger et installer", "Download and install"),
            ["Settings.ConfirmInstallTitle"] = ("Installer la mise à jour", "Install update"),
            ["Settings.ConfirmInstallBody"] = ("Télécharger et installer la version {0} ?\n\nSimHub va redémarrer automatiquement pour appliquer la mise à jour.",
                                                 "Download and install version {0}?\n\nSimHub will restart automatically to apply the update."),
            ["Settings.UpdateReady"] = ("Mise à jour {0} téléchargée — SimHub va redémarrer automatiquement.",
                                          "Update {0} downloaded — SimHub will restart automatically."),
            ["Settings.DownloadFailedTitle"] = ("Erreur", "Error"),
            ["Settings.DownloadFailedBody"] = ("Échec du téléchargement :\n{0}", "Download failed:\n{0}"),
            ["Settings.Log"] = ("LOG", "LOG"),

            ["Settings.RevertStable"] = ("Revenir à la dernière version stable", "Revert to latest stable version"),
            ["Settings.NotStableTitle"] = ("Version de développement détectée", "Development version detected"),
            ["Settings.NotStableBody"] = ("⚠ Attention : vous n'êtes pas sur une version stable publiée (version actuelle : {0}). Voulez-vous revenir à la dernière version stable publiée sur GitHub ({1}) ?",
                                            "⚠ Warning: you are not running a published stable version (current version: {0}). Would you like to revert to the latest stable version published on GitHub ({1})?"),
            ["Settings.ConfirmRevertTitle"] = ("Revenir à une version stable", "Revert to stable version"),
            ["Settings.ConfirmRevertBody"] = ("Revenir à la version stable {0} ?\n\nCette opération remplacera la version actuelle ({1}) et SimHub redémarrera automatiquement.",
                                                "Revert to stable version {0}?\n\nThis will replace the current version ({1}) and SimHub will restart automatically."),
            ["Settings.RevertReady"] = ("Retour à la version {0} téléchargé — SimHub va redémarrer automatiquement.",
                                         "Revert to version {0} downloaded — SimHub will restart automatically."),
            ["Settings.NoStableFound"] = ("Aucune version stable publiée trouvée sur GitHub.", "No published stable version found on GitHub."),
            ["Settings.Download"] = ("Télécharger", "Download"),

            ["Version.Label"] = ("Version : {0}", "Version: {0}"),
            ["Version.NewAvailable"] = ("→ {0} disponible", "→ {0} available"),
            ["Version.UpToDateBadge"] = ("✓ à jour", "✓ up to date"),
            ["Version.DevBadge"] = ("(version de développement)", "(development version)"),

            ["Flash.ConfirmTitle"] = ("Flasher le firmware", "Flash firmware"),
            ["Flash.ConfirmBody"] = ("Le firmware de la pédale va être reflashé.\n\n⚠ Attention : cette opération efface définitivement tout programme actuellement installé sur l'Arduino. Elle est irréversible et le programme existant ne pourra pas être récupéré.\n\nNe débranche pas l'appareil pendant l'opération (environ 15 secondes).\n\nContinuer ?",
                                       "The pedal firmware is about to be reflashed.\n\n⚠ Warning: this operation permanently erases any program currently installed on the Arduino. It is irreversible and the existing program cannot be recovered.\n\nDo not unplug the device during the operation (about 15 seconds).\n\nContinue?"),
            ["Flash.NoPortTitle"] = ("Erreur", "Error"),
            ["Flash.NoPortBody"] = ("Aucun port Arduino détecté.", "No Arduino port detected."),
            ["Flash.SuccessTitle"] = ("Succès", "Success"),
            ["Flash.SuccessBody"] = ("Firmware flashé avec succès.", "Firmware flashed successfully."),
            ["Flash.FailTitle"] = ("Erreur", "Error"),
            ["Flash.FailBody"] = ("Échec du flash :\n{0}", "Flash failed:\n{0}"),
        };
    }
}
