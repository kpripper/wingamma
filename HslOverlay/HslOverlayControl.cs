using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinGamma
{
    internal sealed class HslOverlayControl : UserControl
    {
        private static readonly Color[] BandColors = {
            Color.FromArgb(220, 45, 45),
            Color.FromArgb(235, 125, 35),
            Color.FromArgb(225, 205, 35),
            Color.FromArgb(45, 165, 70),
            Color.FromArgb(40, 185, 185),
            Color.FromArgb(45, 95, 220),
            Color.FromArgb(135, 70, 200),
            Color.FromArgb(215, 55, 155)
        };

        private readonly CheckBox _enabled;
        private readonly Button _reset;
        private readonly Button _test;
        private readonly Label _hint;
        private readonly Label _hueHeader;
        private readonly Label _satHeader;
        private readonly Label _lumHeader;
        private readonly TrackBar[] _hue = new TrackBar[8];
        private readonly TrackBar[] _sat = new TrackBar[8];
        private readonly TrackBar[] _lum = new TrackBar[8];
        private HslBandSettings _settings;
        private bool _loading;
        private bool _runtimeUnavailable;

        public event EventHandler SettingsChanged;
        public event EventHandler TestRequested;

        public HslOverlayControl()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(14);
            AutoScroll = true;
            _settings = HslBandSettings.CreateDefault();

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.ColumnCount = 4;
            layout.RowCount = 11;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));
            Controls.Add(layout);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.AutoSize = true;
            _enabled = new CheckBox();
            _enabled.AutoSize = true;
            _enabled.CheckedChanged += ControlChanged;
            _reset = new Button();
            _reset.AutoSize = true;
            _reset.Click += ResetClicked;
            _test = new Button();
            _test.AutoSize = true;
            _test.Click += delegate
            {
                EventHandler handler = TestRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            };
            actions.Controls.Add(_enabled);
            actions.Controls.Add(_test);
            actions.Controls.Add(_reset);
            layout.Controls.Add(actions, 0, 0);
            layout.SetColumnSpan(actions, 4);

            _hint = new Label();
            _hint.AutoSize = true;
            _hint.MaximumSize = new Size(920, 0);
            _hint.ForeColor = Color.FromArgb(70, 75, 82);
            _hint.Padding = new Padding(0, 4, 0, 10);
            layout.Controls.Add(_hint, 0, 1);
            layout.SetColumnSpan(_hint, 4);

            layout.Controls.Add(new Label(), 0, 2);
            _hueHeader = HeaderLabel();
            _satHeader = HeaderLabel();
            _lumHeader = HeaderLabel();
            layout.Controls.Add(_hueHeader, 1, 2);
            layout.Controls.Add(_satHeader, 2, 2);
            layout.Controls.Add(_lumHeader, 3, 2);

            for (int i = 0; i < 8; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                FlowLayoutPanel band = new FlowLayoutPanel();
                band.Dock = DockStyle.Fill;
                band.FlowDirection = FlowDirection.LeftToRight;
                band.WrapContents = false;
                Panel swatch = new Panel();
                swatch.Size = new Size(22, 22);
                swatch.Margin = new Padding(3, 15, 7, 3);
                swatch.BackColor = BandColors[i];
                Label name = new Label();
                name.AutoSize = true;
                name.Margin = new Padding(0, 17, 0, 0);
                name.Text = _settings.Bands[i].Name;
                band.Controls.Add(swatch);
                band.Controls.Add(name);
                layout.Controls.Add(band, 0, i + 3);

                _hue[i] = NewTrack(-180, 180, 0, 30);
                _sat[i] = NewTrack(0, 200, 100, 25);
                _lum[i] = NewTrack(-100, 100, 0, 20);
                layout.Controls.Add(_hue[i], 1, i + 3);
                layout.Controls.Add(_sat[i], 2, i + 3);
                layout.Controls.Add(_lum[i], 3, i + 3);
            }
            ApplyLanguage();
        }

        public void ApplyLanguage()
        {
            _enabled.Text = Localizer.Get("HslEnable");
            _reset.Text = Localizer.Get("HslReset");
            _test.Text = Localizer.Get("HslSafetyTest");
            _hint.Text = _runtimeUnavailable
                ? Localizer.Get("HslRuntimeUnavailable")
                : Localizer.Get("HslHint");
            _hueHeader.Text = Localizer.Get("HslHue") + " (°)";
            _satHeader.Text = Localizer.Get("HslSat") + " (%)";
            _lumHeader.Text = Localizer.Get("HslLum") + " (%)";
        }

        public void LoadSettings(HslBandSettings settings)
        {
            _loading = true;
            try
            {
                _settings = (settings ?? HslBandSettings.CreateDefault()).Clone();
                _settings.EnsureValid();
                _enabled.Checked = _settings.Enabled;
                for (int i = 0; i < 8; i++)
                {
                    _hue[i].Value = Clamp((int)Math.Round(
                        _settings.Bands[i].HueShiftDeg), -180, 180);
                    _sat[i].Value = Clamp((int)Math.Round(
                        _settings.Bands[i].SaturationScale * 100), 0, 200);
                    _lum[i].Value = Clamp((int)Math.Round(
                        _settings.Bands[i].LuminanceShift * 100), -100, 100);
                }
            }
            finally
            {
                _loading = false;
            }
        }

        public HslBandSettings ReadSettings()
        {
            HslBandSettings result = _settings.Clone();
            result.Enabled = _enabled.Checked;
            for (int i = 0; i < 8; i++)
            {
                HslBand band = result.Bands[i];
                band.HueShiftDeg = _hue[i].Value;
                band.SaturationScale = _sat[i].Value / 100.0f;
                band.LuminanceShift = _lum[i].Value / 100.0f;
                result.Bands[i] = band;
            }
            return result;
        }

        public void SetHdrBlocked(bool blocked)
        {
            Enabled = !blocked;
            if (_runtimeUnavailable)
                _hint.Text = Localizer.Get("HslRuntimeUnavailable");
            else if (blocked)
                _hint.Text = Localizer.Get("HslHdrBlocked");
            else
                _hint.Text = Localizer.Get("HslHint");
        }

        public void SetRuntimeAvailability(bool validated)
        {
            _runtimeUnavailable = !validated;
            _loading = true;
            try
            {
                if (!validated)
                    _enabled.Checked = false;
                _enabled.Enabled = validated;
            }
            finally
            {
                _loading = false;
            }
            _test.Enabled = !validated;
            _test.Visible = !validated;
            _hint.Text = validated
                ? Localizer.Get("HslHint")
                : Localizer.Get("HslRuntimeUnavailable");
        }

        public void SetTestRunning(bool running)
        {
            _test.Enabled = !running;
            _test.Text = running
                ? Localizer.Get("HslSafetyTesting")
                : Localizer.Get("HslSafetyTest");
        }

        private void ResetClicked(object sender, EventArgs e)
        {
            HslBandSettings reset = HslBandSettings.CreateDefault();
            reset.Enabled = _enabled.Checked;
            LoadSettings(reset);
            RaiseChanged();
        }

        private void ControlChanged(object sender, EventArgs e)
        {
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (_loading)
                return;
            EventHandler handler = SettingsChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private TrackBar NewTrack(int minimum, int maximum, int value,
            int frequency)
        {
            TrackBar track = new TrackBar();
            track.Dock = DockStyle.Fill;
            track.Minimum = minimum;
            track.Maximum = maximum;
            track.Value = value;
            track.TickFrequency = frequency;
            track.SmallChange = Math.Max(1, frequency / 5);
            track.LargeChange = frequency;
            track.Scroll += ControlChanged;
            return track;
        }

        private static Label HeaderLabel()
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            return label;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
