using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PneumaticCalibratorSimHub
{
    public partial class SettingsControl : UserControl
    {
        private readonly PedalSerial _serial = new PedalSerial();
        private ChannelPanel[] _panels;
        private readonly bool[] _channelConnected = { true, true, true, true };
        private bool _showAllAxes;
        private DispatcherTimer _scopeTimer;
        private bool _flashing;
        private PluginUpdater.UpdateInfo _pendingUpdate;
        private bool _checkingUpdate;

        public SettingsControl()
        {
            InitializeComponent();
            _panels = new[] { Panel0, Panel1, Panel2, Panel3 };
            for (int ch = 0; ch < _panels.Length; ch++)
            {
                _panels[ch].Initialize(ch, PedalSerial.ChannelNames[ch], PedalSerial.ChannelPins[ch]);
                _panels[ch].SetMinRequested += c => _serial.SetMin(c);
                _panels[ch].SetMaxRequested += c => _serial.SetMax(c);
                _panels[ch].DeadzoneMinChanged += (c, v) => _serial.SetDeadzoneMin(c, v);
                _panels[ch].DeadzoneMaxChanged += (c, v) => _serial.SetDeadzoneMax(c, v);
            }

            _serial.Log += line => Dispatcher.Invoke(() => AppendLog(line));
            _serial.RawOutUpdated += (ch, raw, output) => Dispatcher.Invoke(() => _panels[ch].UpdateRawOut(raw, output));
            _serial.ConfigUpdated += (ch, kind, value) => Dispatcher.Invoke(() => _panels[ch].UpdateConfig(kind, value));
            _serial.ChannelConnectivityChanged += (ch, conn) => Dispatcher.Invoke(() => OnChannelConnectivityChanged(ch, conn));
            _serial.Disconnected += () => Dispatcher.Invoke(OnDisconnected);

            _scopeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _scopeTimer.Tick += (s, e) => { foreach (var p in _panels) p.OnScopeTick(); };
            _scopeTimer.Start();

            LblCurrentVersion.Text = $"Version actuelle : {PluginUpdater.CurrentVersion}";
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_checkingUpdate) return;
            _checkingUpdate = true;
            BtnCheckUpdate.IsEnabled = false;
            LblUpdateStatus.Visibility = Visibility.Visible;
            LblUpdateStatus.Text = "Vérification en cours...";
            BtnInstallUpdate.Visibility = Visibility.Collapsed;
            _pendingUpdate = null;

            try
            {
                var update = await PluginUpdater.CheckForUpdateAsync();
                if (update == null)
                {
                    LblUpdateStatus.Text = "Vous avez déjà la dernière version.";
                }
                else
                {
                    _pendingUpdate = update;
                    LblUpdateStatus.Text = $"Nouvelle version disponible : {update.Version}";
                    BtnInstallUpdate.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                LblUpdateStatus.Text = $"Échec de la vérification : {ex.Message}";
                AppendLog($"[update] échec vérification : {ex.Message}");
            }
            finally
            {
                _checkingUpdate = false;
                BtnCheckUpdate.IsEnabled = true;
            }
        }

        private async void BtnInstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate == null) return;

            var confirm = MessageBox.Show(
                $"Télécharger et installer la version {_pendingUpdate.Version} ?\n\n" +
                "La mise à jour sera appliquée automatiquement à la prochaine fermeture de SimHub.",
                "Installer la mise à jour", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            BtnInstallUpdate.IsEnabled = false;
            try
            {
                await PluginUpdater.DownloadAndScheduleInstallAsync(_pendingUpdate, line => Dispatcher.Invoke(() => AppendLog(line)));
                LblUpdateStatus.Text = $"Mise à jour {_pendingUpdate.Version} prête — sera installée à la prochaine fermeture de SimHub.";
                BtnInstallUpdate.Visibility = Visibility.Collapsed;
                _pendingUpdate = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Échec du téléchargement :\n{ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog($"[update] échec téléchargement : {ex.Message}");
            }
            finally
            {
                BtnInstallUpdate.IsEnabled = true;
            }
        }

        private void OnChannelConnectivityChanged(int ch, bool connected)
        {
            _channelConnected[ch] = connected;
            _panels[ch].SetConnectivity(connected);
            _panels[ch].SetVisible(connected || _showAllAxes);
        }

        private void ChkShowAllAxes_Checked(object sender, RoutedEventArgs e) => SetShowAllAxes(true);
        private void ChkShowAllAxes_Unchecked(object sender, RoutedEventArgs e) => SetShowAllAxes(false);

        private void SetShowAllAxes(bool show)
        {
            _showAllAxes = show;
            for (int ch = 0; ch < _panels.Length; ch++)
                _panels[ch].SetVisible(_channelConnected[ch] || _showAllAxes);
        }

        private void AppendLog(string line)
        {
            TxtLog.AppendText(line + "\n");
            if (TxtLog.Text.Length > 8000) TxtLog.Clear();
            TxtLog.ScrollToEnd();
        }

        private void CmbPort_DropDownOpened(object sender, EventArgs e)
        {
            var ports = PedalSerial.ListArduinoPorts();
            CmbPort.Items.Clear();
            if (ports.Count == 0)
            {
                CmbPort.Items.Add("Aucun appareil détecté");
                CmbPort.SelectedIndex = 0;
                return;
            }
            foreach (var p in ports) CmbPort.Items.Add($"{p.Port} — {p.Label}");
            CmbPort.SelectedIndex = 0;
        }

        private string SelectedPort()
        {
            var text = CmbPort.SelectedItem as string;
            if (string.IsNullOrEmpty(text)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(text, @"COM\d+");
            return m.Success ? m.Value : null;
        }

        private void CmbFlashPort_DropDownOpened(object sender, EventArgs e)
        {
            var ports = PedalSerial.ListArduinoPorts(); // déjà filtré sur Arduino/Pro Micro/Leonardo
            CmbFlashPort.Items.Clear();
            if (ports.Count == 0)
            {
                CmbFlashPort.Items.Add("Aucun appareil détecté");
                CmbFlashPort.SelectedIndex = 0;
                return;
            }
            foreach (var p in ports) CmbFlashPort.Items.Add($"{p.Port} — {p.Label}");
            CmbFlashPort.SelectedIndex = 0;
        }

        private string SelectedFlashPort()
        {
            var text = CmbFlashPort.SelectedItem as string;
            if (string.IsNullOrEmpty(text)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(text, @"COM\d+");
            return m.Success ? m.Value : null;
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_serial.IsConnected) { _serial.Disconnect(); OnDisconnected(); return; }

            string port = SelectedPort();
            if (port == null)
            {
                var found = PedalSerial.ListArduinoPorts();
                if (found.Count > 0) port = found[0].Port;
            }
            if (port == null)
            {
                LblStatus.Content = "● Arduino introuvable";
                LblStatus.Foreground = Brushes.OrangeRed;
                return;
            }

            try
            {
                _serial.Connect(port);
                SetConnectedUi(port);
            }
            catch (Exception ex)
            {
                LblStatus.Content = $"● Erreur : {ex.Message}";
                LblStatus.Foreground = Brushes.OrangeRed;
            }
        }

        private void SetConnectedUi(string port)
        {
            LblStatus.Content = $"● Connecté   {port}";
            LblStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            BtnConnect.Content = "Déconnecter";
            ChannelsGrid.Visibility = Visibility.Visible;
            foreach (var p in _panels) p.SetEnabledForConnection(true);
        }

        private void OnDisconnected()
        {
            LblStatus.Content = "● Déconnecté";
            LblStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            BtnConnect.Content = "Connecter";
            ChannelsGrid.Visibility = Visibility.Collapsed;
            for (int ch = 0; ch < _panels.Length; ch++)
            {
                _panels[ch].SetEnabledForConnection(false);
                _panels[ch].ResetReadout();
                _channelConnected[ch] = true; // on ne sait plus rien : redevient visible par défaut
                _panels[ch].SetConnectivity(true);
                _panels[ch].SetVisible(true);
            }
        }

        private async void BtnFlash_Click(object sender, RoutedEventArgs e)
        {
            if (_flashing) return;

            var confirm = MessageBox.Show(
                "Le firmware de la pédale va être reflashé.\n\n" +
                "⚠ Attention : cette opération efface définitivement tout programme actuellement installé sur l'Arduino. " +
                "Elle est irréversible et le programme existant ne pourra pas être récupéré.\n\n" +
                "Ne débranche pas l'appareil pendant l'opération (environ 15 secondes).\n\nContinuer ?",
                "Flasher le firmware", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            string port = SelectedFlashPort();
            if (port == null)
            {
                var found = PedalSerial.ListArduinoPorts();
                if (found.Count > 0) port = found[0].Port;
            }
            if (port == null)
            {
                MessageBox.Show("Aucun port Arduino détecté.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool wasConnected = _serial.IsConnected;
            if (wasConnected) { _serial.Disconnect(); OnDisconnected(); }

            _flashing = true;
            BtnFlash.IsEnabled = false;
            BtnFlash.Content = "FLASH EN COURS...";
            AppendLog("=== Flash firmware démarré ===");

            try
            {
                await FirmwareFlasher.FlashAsync(port, line => Dispatcher.Invoke(() => AppendLog(line)));
                AppendLog("=== Flash terminé avec succès ===");
                MessageBox.Show("Firmware flashé avec succès.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);

                await Task.Delay(1500);
                try { _serial.Connect(port); SetConnectedUi(port); } catch { }
            }
            catch (Exception ex)
            {
                AppendLog($"=== ERREUR : {ex.Message} ===");
                MessageBox.Show($"Échec du flash :\n{ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _flashing = false;
                BtnFlash.IsEnabled = true;
                BtnFlash.Content = "⚡ Flasher firmware";
            }
        }

        public void Shutdown()
        {
            _scopeTimer?.Stop();
            _serial.Disconnect();
        }
    }
}
