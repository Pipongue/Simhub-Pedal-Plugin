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

        public ImageSource PictureIcon => null;

        private SettingsControl _control;

        public void Init(PluginManager pluginManager)
        {
            // Doit s'exécuter avant toute utilisation de PedalSerial/SerialPort dans ce plugin :
            // extrait les DLL de dépendances embarquées si elles manquent (distribution = 1 seul fichier).
            DependencyBootstrapper.EnsureDependenciesExtracted();
            PluginManager = pluginManager;

            if (_control != null)
            {
                // SimHub appelle End()/Init() à chaque changement de jeu, ce qui ferme puis tente
                // de rouvrir la connexion du panneau de calibration. On la rétablit automatiquement.
                _control.Resume();
            }
            else
            {
                // Premier démarrage de SimHub, panneau de calibration jamais ouvert : on ouvre puis
                // referme brièvement le port série en tâche de fond, pour éviter aux utilisateurs
                // d'avoir à ouvrir le plugin manuellement avant que les axes soient pris en compte.
                System.Threading.Tasks.Task.Run(() => PedalSerial.AutoConnectPulse());
            }
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
