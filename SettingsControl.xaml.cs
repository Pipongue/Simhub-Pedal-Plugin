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
        private bool _isConnected;
        private string _connectedPort;
        private bool _suppressLangEvent;

        private enum VersionState { Unknown, Dev, UpToDate, Outdated }
        private VersionState _versionState = VersionState.Unknown;
        private bool _devBuildStatusShown;

        public SettingsControl()
        {
            Localization.Load();
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

            Localization.LanguageChanged += ApplyLocalization;
            ApplyLocalization();

            _ = RefreshVersionStatusAsync();
        }

        private async Task RefreshVersionStatusAsync()
        {
            if (PluginUpdater.IsRunningDevBuild)
            {
                _versionState = VersionState.Dev;
                ApplyVersionArrowText();
                return;
            }
            try
            {
                var update = await PluginUpdater.CheckForUpdateAsync();
                _pendingUpdate = update;
                _versionState = update != null ? VersionState.Outdated : VersionState.UpToDate;
                ApplyVersionArrowText();
            }
            catch { }
        }

        private void ApplyVersionArrowText()
        {
            LblVersionTop.Text = Localization.T("Version.Label", PluginUpdater.CurrentVersion);
            LblVersionArrow.Visibility = Visibility.Collapsed;
            BtnDownloadTop.Visibility = Visibility.Collapsed;
            BtnRevertTop.Visibility = Visibility.Collapsed;
            LblVersionTop.Foreground = Brushes.White;

            switch (_versionState)
            {
                case VersionState.Dev:
                    LblVersionArrow.Visibility = Visibility.Visible;
                    LblVersionArrow.Text = Localization.T("Version.DevBadge");
                    LblVersionArrow.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    BtnRevertTop.Visibility = Visibility.Visible;
                    break;
                case VersionState.Outdated:
                    LblVersionArrow.Visibility = Visibility.Visible;
                    LblVersionArrow.Text = Localization.T("Version.NewAvailable", _pendingUpdate?.Version);
                    LblVersionArrow.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    BtnDownloadTop.Visibility = Visibility.Visible;
                    break;
                case VersionState.UpToDate:
                    LblVersionTop.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    break;
            }

            if (_devBuildStatusShown)
                LblUpdateStatus.Text = Localization.T("Settings.DevBuildStatus", PluginUpdater.CurrentVersion);
        }

        private void ApplyLocalization()
        {
            TabCalibration.Header = Localization.T("Tab.Calibration");
            TabFlash.Header = Localization.T("Tab.Flash");
            TabSettings.Header = Localization.T("Tab.Settings");

            BtnConnect.Content = Localization.T(_isConnected ? "Disconnect" : "Connect");
            LblStatus.Content = _isConnected
                ? Localization.T("Status.Connected", _connectedPort)
                : Localization.T("Status.Disconnected");
            TxtNoDeviceHint.Text = Localization.T("Calibration.NoDeviceHint");
            BorderNoDeviceHint.Visibility = _isConnected ? Visibility.Collapsed : Visibility.Visible;

            RunFlashWarningTitle.Text = Localization.T("Flash.Warning");
            RunFlashWarningBody.Text = Localization.T("Flash.WarningBody");
            SecHardware.Title = Localization.T("Flash.HardwareRequired");
            RunBoardLine1.Text = Localization.T("Flash.BoardLine1");
            RunBoardBold.Text = Localization.T("Flash.BoardBold");
            RunBoardLine1End.Text = Localization.T("Flash.BoardLine1End");
            TxtBoardNote.Text = Localization.T("Flash.BoardNote");
            SecSensors.Title = Localization.T("Flash.CompatibleSensors");
            TxtSensorsIntro.Text = Localization.T("Flash.SensorsIntro");
            TxtCompatibleTitle.Text = Localization.T("Flash.Compatible");
            TxtCompatibleList.Text = Localization.T("Flash.CompatibleList");
            TxtIncompatibleTitle.Text = Localization.T("Flash.Incompatible");
            TxtIncompatibleList.Text = Localization.T("Flash.IncompatibleList");
            SecWiring.Title = Localization.T("Flash.Wiring");
            TxtWiringIntro.Text = Localization.T("Flash.WiringIntro");
            TxtPinA0.Text = Localization.T("Channel.Handbrake");
            TxtPinA1.Text = Localization.T("Channel.Throttle");
            TxtPinA2.Text = Localization.T("Channel.Brake");
            TxtPinA3.Text = Localization.T("Channel.Clutch");
            TxtWiringNote.Text = Localization.T("Flash.WiringNote");
            SecProcedure.Title = Localization.T("Flash.Procedure");
            TxtProcedureSteps.Text = Localization.T("Flash.ProcedureSteps");
            BtnFlash.Content = _flashing ? Localization.T("Flash.ButtonInProgress") : Localization.T("Flash.Button");

            SecDevOptions.Title = Localization.T("Settings.DevOptions");
            ChkShowAllAxes.Content = Localization.T("Settings.ShowAllAxes");
            SecLanguage.Title = Localization.T("Settings.Language");
            _suppressLangEvent = true;
            CmbLanguage.Items.Clear();
            CmbLanguage.Items.Add(Localization.T("Settings.LangFr"));
            CmbLanguage.Items.Add(Localization.T("Settings.LangEn"));
            CmbLanguage.SelectedIndex = Localization.Current == Lang.En ? 1 : 0;
            _suppressLangEvent = false;
            SecUpdate.Title = Localization.T("Settings.Update");
            BtnCheckUpdate.Content = Localization.T("Settings.CheckUpdate");
            BtnDownloadTop.Content = Localization.T("Settings.Download");
            BtnRevertTop.Content = Localization.T("Settings.RevertStable");
            BtnRevertStable.Content = Localization.T("Settings.RevertStable");
            SecLog.Title = Localization.T("Settings.Log");

            for (int ch = 0; ch < _panels.Length; ch++)
                _panels[ch].ApplyLocalization(PedalSerial.ChannelNames[ch]);

            ApplyVersionArrowText();
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLangEvent) return;
            Localization.SetLanguage(CmbLanguage.SelectedIndex == 1 ? Lang.En : Lang.Fr);
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_checkingUpdate) return;
            _checkingUpdate = true;
            BtnCheckUpdate.IsEnabled = false;
            LblUpdateStatus.Visibility = Visibility.Visible;
            LblUpdateStatus.Text = Localization.T("Settings.Checking");
            _pendingUpdate = null;

            try
            {
                var update = await PluginUpdater.CheckForUpdateAsync();
                if (update == null)
                {
                    _devBuildStatusShown = PluginUpdater.IsRunningDevBuild;
                    LblUpdateStatus.Text = PluginUpdater.IsRunningDevBuild
                        ? Localization.T("Settings.DevBuildStatus", PluginUpdater.CurrentVersion)
                        : Localization.T("Settings.UpToDate");
                    _versionState = PluginUpdater.IsRunningDevBuild ? VersionState.Dev : VersionState.UpToDate;
                    ApplyVersionArrowText();

                    if (PluginUpdater.IsRunningDevBuild)
                    {
                        var stable = await PluginUpdater.GetLatestStableReleaseAsync();
                        if (stable != null)
                        {
                            var confirm = MessageBox.Show(
                                Localization.T("Settings.NotStableBody", PluginUpdater.CurrentVersion, stable.Version),
                                Localization.T("Settings.NotStableTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (confirm == MessageBoxResult.Yes)
                                await RevertToStableAsync(stable);
                        }
                    }
                }
                else
                {
                    _devBuildStatusShown = false;
                    _pendingUpdate = update;
                    LblUpdateStatus.Text = Localization.T("Settings.NewVersion", update.Version);
                    _versionState = VersionState.Outdated;
                    ApplyVersionArrowText();
                }
            }
            catch (Exception ex)
            {
                _devBuildStatusShown = false;
                LblUpdateStatus.Text = Localization.T("Settings.CheckFailed", ex.Message);
                AppendLog($"[update] échec vérification : {ex.Message}");
            }
            finally
            {
                _checkingUpdate = false;
                BtnCheckUpdate.IsEnabled = true;
            }
        }

        private async void BtnRevertStable_Click(object sender, RoutedEventArgs e)
        {
            BtnRevertStable.IsEnabled = false;
            BtnRevertTop.IsEnabled = false;
            try
            {
                var stable = await PluginUpdater.GetLatestStableReleaseAsync();
                if (stable == null)
                {
                    MessageBox.Show(Localization.T("Settings.NoStableFound"), Localization.T("Settings.ConfirmRevertTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirm = MessageBox.Show(
                    Localization.T("Settings.ConfirmRevertBody", stable.Version, PluginUpdater.CurrentVersion),
                    Localization.T("Settings.ConfirmRevertTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                await RevertToStableAsync(stable);
            }
            finally
            {
                BtnRevertStable.IsEnabled = true;
                BtnRevertTop.IsEnabled = true;
            }
        }

        private async Task RevertToStableAsync(PluginUpdater.UpdateInfo stable)
        {
            try
            {
                await PluginUpdater.DownloadAndScheduleInstallAsync(stable, line => Dispatcher.Invoke(() => AppendLog(line)));
                LblUpdateStatus.Visibility = Visibility.Visible;
                LblUpdateStatus.Text = Localization.T("Settings.RevertReady", stable.Version);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Localization.T("Settings.DownloadFailedBody", ex.Message), Localization.T("Settings.DownloadFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog($"[update] échec téléchargement : {ex.Message}");
            }
        }

        private async void BtnInstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate == null) return;

            var confirm = MessageBox.Show(
                Localization.T("Settings.ConfirmInstallBody", _pendingUpdate.Version),
                Localization.T("Settings.ConfirmInstallTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            BtnDownloadTop.IsEnabled = false;
            try
            {
                await PluginUpdater.DownloadAndScheduleInstallAsync(_pendingUpdate, line => Dispatcher.Invoke(() => AppendLog(line)));
                LblUpdateStatus.Visibility = Visibility.Visible;
                LblUpdateStatus.Text = Localization.T("Settings.UpdateReady", _pendingUpdate.Version);
                BtnDownloadTop.Visibility = Visibility.Collapsed;
                _pendingUpdate = null;
                _versionState = VersionState.Unknown;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Localization.T("Settings.DownloadFailedBody", ex.Message), Localization.T("Settings.DownloadFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog($"[update] échec téléchargement : {ex.Message}");
            }
            finally
            {
                BtnDownloadTop.IsEnabled = true;
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
                CmbPort.Items.Add(Localization.T("NoDeviceFound"));
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
                CmbFlashPort.Items.Add(Localization.T("NoDeviceFound"));
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
                LblStatus.Content = Localization.T("Status.NotFound");
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
                LblStatus.Content = Localization.T("Status.Error", ex.Message);
                LblStatus.Foreground = Brushes.OrangeRed;
            }
        }

        private void SetConnectedUi(string port)
        {
            _isConnected = true;
            _connectedPort = port;
            LblStatus.Content = Localization.T("Status.Connected", port);
            LblStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            BtnConnect.Content = Localization.T("Disconnect");
            BorderNoDeviceHint.Visibility = Visibility.Collapsed;
            ChannelsGrid.Visibility = Visibility.Visible;
            foreach (var p in _panels) p.SetEnabledForConnection(true);
        }

        private void OnDisconnected()
        {
            _isConnected = false;
            _connectedPort = null;
            LblStatus.Content = Localization.T("Status.Disconnected");
            LblStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            BtnConnect.Content = Localization.T("Connect");
            BorderNoDeviceHint.Visibility = Visibility.Visible;
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
                Localization.T("Flash.ConfirmBody"),
                Localization.T("Flash.ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            string port = SelectedFlashPort();
            if (port == null)
            {
                var found = PedalSerial.ListArduinoPorts();
                if (found.Count > 0) port = found[0].Port;
            }
            if (port == null)
            {
                MessageBox.Show(Localization.T("Flash.NoPortBody"), Localization.T("Flash.NoPortTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool wasConnected = _serial.IsConnected;
            if (wasConnected) { _serial.Disconnect(); OnDisconnected(); }

            _flashing = true;
            BtnFlash.IsEnabled = false;
            BtnFlash.Content = Localization.T("Flash.ButtonInProgress");
            AppendLog("=== Flash firmware démarré ===");

            try
            {
                await FirmwareFlasher.FlashAsync(port, line => Dispatcher.Invoke(() => AppendLog(line)));
                AppendLog("=== Flash terminé avec succès ===");
                MessageBox.Show(Localization.T("Flash.SuccessBody"), Localization.T("Flash.SuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);

                await Task.Delay(1500);
                try { _serial.Connect(port); SetConnectedUi(port); } catch { }
            }
            catch (Exception ex)
            {
                AppendLog($"=== ERREUR : {ex.Message} ===");
                MessageBox.Show(Localization.T("Flash.FailBody", ex.Message), Localization.T("Flash.FailTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _flashing = false;
                BtnFlash.IsEnabled = true;
                BtnFlash.Content = Localization.T("Flash.Button");
            }
        }

        public void Shutdown()
        {
            Localization.LanguageChanged -= ApplyLocalization;
            _scopeTimer?.Stop();
            _serial.Disconnect();
        }
    }
}
