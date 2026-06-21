using System.Windows.Media;
using SimHub.Plugins;

namespace PneumaticCalibratorSimHub
{
    [PluginDescription("Calibration des pédales pneumatiques (4 canaux fixes par pin)")]
    [PluginAuthor("Pierre Gleizes")]
    [PluginName("Pneumatic Pedal Calibrator")]
    public class PedalCalibratorPlugin : IPlugin, IWPFSettingsV2
    {
        public PluginManager PluginManager { get; set; }

        public string LeftMenuTitle => "Pneumatic Pedals";

        public ImageSource PictureIcon => LogoLoader.Icon;

        private SettingsControl _control;

        public void Init(PluginManager pluginManager)
        {
            // Doit s'exécuter avant toute utilisation de PedalSerial/SerialPort dans ce plugin :
            // extrait les DLL de dépendances embarquées si elles manquent (distribution = 1 seul fichier).
            DependencyBootstrapper.EnsureDependenciesExtracted();
            PluginManager = pluginManager;
        }

        public void End(PluginManager pluginManager)
        {
            _control?.Shutdown();
        }

        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            if (_control == null) _control = new SettingsControl();
            return _control;
        }
    }
}
