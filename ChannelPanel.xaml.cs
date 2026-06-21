using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PneumaticCalibratorSimHub
{
    public partial class ChannelPanel : UserControl
    {
        public int ChannelIndex { get; private set; }

        public event Action<int> SetMinRequested;
        public event Action<int> SetMaxRequested;
        public event Action<int, int> DeadzoneMinChanged; // ch, value
        public event Action<int, int> DeadzoneMaxChanged; // ch, value

        private bool _suppressDzEvents;
        private bool _connected = true;

        private const int MaxTracePoints = 120;
        private readonly Queue<int> _trace = new Queue<int>();
        private int _latestOutput;

        public ChannelPanel()
        {
            InitializeComponent();

            // TitledRangeSlider est un contrôle natif SimHub ; on observe ses propriétés de
            // dépendance par réflexion pour rester robuste si la signature exacte d'event diffère.
            HookDependencyProperty(DzSlider, "LowerValueProperty", (s, e) => OnLowerValueChanged());
            HookDependencyProperty(DzSlider, "UpperValueProperty", (s, e) => OnUpperValueChanged());
        }

        private static void HookDependencyProperty(DependencyObject obj, string propName, EventHandler handler)
        {
            var field = obj.GetType().GetField(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (field?.GetValue(null) is DependencyProperty dp)
            {
                DependencyPropertyDescriptor.FromProperty(dp, obj.GetType())?.AddValueChanged(obj, handler);
            }
        }

        public void Initialize(int channelIndex, string name, string pin)
        {
            ChannelIndex = channelIndex;
            RootSection.Title = name;
            LblPin.Text = pin;
            ApplyLocalization(name);
        }

        public void ApplyLocalization(string name)
        {
            RootSection.Title = name;
            LblRaw.Text = Localization.T("Raw");
            LblOutput.Text = Localization.T("Output");
            BtnSetMin.Content = Localization.T("SetMin");
            BtnSetMax.Content = Localization.T("SetMax");
            DzSlider.Title = Localization.T("Deadzone");
        }

        public void SetShowRaw(bool show)
        {
            var visibility = show ? Visibility.Visible : Visibility.Collapsed;
            LblRaw.Visibility = visibility;
            LblRawVal.Visibility = visibility;
            BarRaw.Visibility = visibility;
        }

        public void SetEnabledForConnection(bool connected)
        {
            BtnSetMin.IsEnabled = connected;
            BtnSetMax.IsEnabled = connected;
            DzSlider.IsEnabled = connected;
        }

        public void SetConnectivity(bool connected)
        {
            _connected = connected;

            // Gèle/remet à zéro l'affichage pour ne pas montrer le bruit d'une pin
            // non branchée ; les vraies valeurs reviendront dès la reconnexion.
            if (!connected) ZeroOut();
        }

        // Indépendant de SetConnectivity : contrôlé par l'appelant (caché si déconnecté,
        // sauf si l'option dev "Afficher tous les axes" est active).
        public void SetVisible(bool visible)
        {
            // Le UniformGrid parent recalcule ses colonnes en fonction des enfants visibles
            // (Rows="2" fixe), donc la grille se réorganise automatiquement.
            Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ZeroOut()
        {
            BarRaw.Value = 0; BarOut.Value = 0;
            LblRawVal.Text = LblOutVal.Text = "0";
            _trace.Clear();
            _latestOutput = 0;
            ScopeTrace.Points = new PointCollection();
        }

        public void ResetReadout()
        {
            BarRaw.Value = 0; BarOut.Value = 0;
            LblRawVal.Text = LblOutVal.Text = "—";
            _trace.Clear();
            _latestOutput = 0;
            ScopeTrace.Points = new PointCollection();
        }

        public void UpdateRawOut(int raw, int output)
        {
            if (!_connected) return; // ignore le bruit tant que le canal est marqué déconnecté
            BarRaw.Value = raw;
            LblRawVal.Text = raw.ToString();
            BarOut.Value = output;
            LblOutVal.Text = output.ToString();
            _latestOutput = output;
        }

        public void UpdateConfig(string kind, int value)
        {
            _suppressDzEvents = true;
            switch (kind)
            {
                case "DZN": DzSlider.LowerValue = value; break;
                case "DZX": DzSlider.UpperValue = 100 - value; break;
            }
            _suppressDzEvents = false;
        }

        private void OnLowerValueChanged()
        {
            if (_suppressDzEvents) return;
            DeadzoneMinChanged?.Invoke(ChannelIndex, (int)DzSlider.LowerValue);
        }

        private void OnUpperValueChanged()
        {
            if (_suppressDzEvents) return;
            DeadzoneMaxChanged?.Invoke(ChannelIndex, (int)(100 - DzSlider.UpperValue));
        }

        public void OnScopeTick()
        {
            _trace.Enqueue(_latestOutput);
            while (_trace.Count > MaxTracePoints) _trace.Dequeue();
            RedrawScope();
        }

        private void RedrawScope()
        {
            double w = ScopeCanvas.ActualWidth, h = ScopeCanvas.ActualHeight;
            if (w < 4 || h < 4 || _trace.Count < 2) return;

            double dx = (w - 1) / (MaxTracePoints - 1);
            int n = _trace.Count;
            var points = new PointCollection(n);
            int i = 0;
            foreach (var v in _trace)
            {
                double x = (w - 1) - (n - 1 - i) * dx;
                int clamped = v < 0 ? 0 : (v > 1023 ? 1023 : v);
                double y = (h - 1) - clamped / 1023.0 * (h - 1);
                points.Add(new Point(x, y));
                i++;
            }
            ScopeTrace.Points = points;
        }

        private void ScopeCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double w = ScopeCanvas.ActualWidth, h = ScopeCanvas.ActualHeight;
            SetGridLine(GridLine25, w, h * 0.25);
            SetGridLine(GridLine50, w, h * 0.5);
            SetGridLine(GridLine75, w, h * 0.75);
            RedrawScope();
        }

        private static void SetGridLine(Line line, double w, double y)
        {
            line.X1 = 0; line.X2 = w; line.Y1 = y; line.Y2 = y;
        }

        private void BtnSetMin_Click(object sender, RoutedEventArgs e) => SetMinRequested?.Invoke(ChannelIndex);
        private void BtnSetMax_Click(object sender, RoutedEventArgs e) => SetMaxRequested?.Invoke(ChannelIndex);
    }
}
