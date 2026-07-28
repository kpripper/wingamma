using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace WinGamma
{
    public sealed class MainForm : Form
    {
        private sealed class MonitorSession
        {
            public DisplayMonitor Monitor;
            public ProfileContext Context;
            public GammaSettings Settings;
            public GammaRamp RestoreRamp;
            public HslBandSettings HslSettings;
        }

        private readonly AppSettings _appSettings;
        private readonly Dictionary<string, MonitorSession> _sessions;
        private readonly Dictionary<string, Control> _localized;
        private readonly Timer _previewTimer;
        private readonly HslOverlayManager _hslManager;
        private List<DisplayMonitor> _monitors;
        private bool _loading;

        private ComboBox _monitorCombo;
        private ComboBox _languageCombo;
        private ComboBox _targetCombo;
        private CheckBox _linkCheck;
        private CheckBox _autostartCheck;
        private TrackBar _gammaRed;
        private TrackBar _gammaGreen;
        private TrackBar _gammaBlue;
        private TrackBar _brightness;
        private TrackBar _contrast;
        private TrackBar _temperature;
        private NumericUpDown _gammaRedValue;
        private NumericUpDown _gammaGreenValue;
        private NumericUpDown _gammaBlueValue;
        private NumericUpDown _brightnessValue;
        private NumericUpDown _contrastValue;
        private NumericUpDown _temperatureValue;
        private Label _status;
        private TestPatternControl _pattern;
        private Button _installButton;
        private TableLayoutPanel _slidersPanel;
        private TabControl _tabs;
        private TabPage _calibrationTab;
        private TabPage _hslTab;
        private HslOverlayControl _hslControl;

        public MainForm()
        {
            _appSettings = SettingsStore.Load();
            Localizer.Language = _appSettings.Language;
            _sessions = new Dictionary<string, MonitorSession>(
                StringComparer.OrdinalIgnoreCase);
            _localized = new Dictionary<string, Control>(
                StringComparer.OrdinalIgnoreCase);
            _previewTimer = new Timer();
            _hslManager = new HslOverlayManager();
            _previewTimer.Interval = 90;
            _previewTimer.Tick += PreviewTimerTick;

            InitializeWindow();
            PopulateMonitors();
            ApplyLanguage();
            FormClosing += MainFormClosing;
        }

        private void InitializeWindow()
        {
            Text = Localizer.Get("AppTitle");
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(880, 660);
            Size = new Size(1080, 760);
            Font = new Font("Segoe UI", 9.0f);
            BackColor = Color.FromArgb(245, 246, 248);

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _calibrationTab = new TabPage();
            _calibrationTab.BackColor = BackColor;
            _hslTab = new TabPage();
            _hslTab.BackColor = BackColor;
            _tabs.TabPages.Add(_calibrationTab);
            _tabs.TabPages.Add(_hslTab);
            Controls.Add(_tabs);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 2;
            root.RowCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _calibrationTab.Controls.Add(root);

            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            left.AutoScroll = true;
            root.Controls.Add(left, 0, 0);

            FlowLayoutPanel leftFlow = new FlowLayoutPanel();
            leftFlow.Dock = DockStyle.Top;
            leftFlow.AutoSize = true;
            leftFlow.FlowDirection = FlowDirection.TopDown;
            leftFlow.WrapContents = false;
            leftFlow.Padding = new Padding(0, 0, 12, 0);
            left.Controls.Add(leftFlow);

            _monitorCombo = NewCombo(false);
            _monitorCombo.SelectedIndexChanged += MonitorChanged;
            leftFlow.Controls.Add(NewHeaderRow("Monitor", _monitorCombo));

            _languageCombo = NewCombo(true);
            _languageCombo.Items.Add("Українська");
            _languageCombo.Items.Add("English");
            _languageCombo.SelectedIndex =
                Localizer.Language == "en" ? 1 : 0;
            _languageCombo.SelectedIndexChanged += LanguageChanged;
            leftFlow.Controls.Add(NewHeaderRow("Language", _languageCombo));

            _targetCombo = NewCombo(false);
            _targetCombo.DropDownStyle = ComboBoxStyle.DropDown;
            _targetCombo.Items.AddRange(new object[] { "1.8", "2.2", "2.4" });
            _targetCombo.Text = "2.2";
            _targetCombo.Validating += delegate { ReadTargetGamma(true); };
            _targetCombo.TextChanged += SettingsChanged;
            leftFlow.Controls.Add(NewHeaderRow("TargetGamma", _targetCombo));

            _linkCheck = new CheckBox();
            _linkCheck.AutoSize = true;
            _linkCheck.Margin = new Padding(3, 8, 3, 8);
            _linkCheck.Checked = true;
            _linkCheck.CheckedChanged += LinkChanged;
            Register("LinkRgb", _linkCheck);
            leftFlow.Controls.Add(_linkCheck);

            _slidersPanel = new TableLayoutPanel();
            _slidersPanel.Width = 412;
            _slidersPanel.AutoSize = true;
            _slidersPanel.ColumnCount = 3;
            _slidersPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
            _slidersPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _slidersPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            leftFlow.Controls.Add(_slidersPanel);

            _gammaRed = NewTrack(50, 250, 100, 25);
            _gammaGreen = NewTrack(50, 250, 100, 25);
            _gammaBlue = NewTrack(50, 250, 100, 25);
            _brightness = NewTrack(-20, 20, 0, 5);
            _contrast = NewTrack(50, 150, 100, 10);
            _temperature = NewTrack(3500, 10000, 6500, 500);

            _gammaRedValue = AddSliderRow(0, "GammaRed", _gammaRed,
                2, 0.50m, 2.50m, 0.01m);
            _gammaGreenValue = AddSliderRow(1, "GammaGreen", _gammaGreen,
                2, 0.50m, 2.50m, 0.01m);
            _gammaBlueValue = AddSliderRow(2, "GammaBlue", _gammaBlue,
                2, 0.50m, 2.50m, 0.01m);
            _brightnessValue = AddSliderRow(3, "Brightness", _brightness,
                0, -20m, 20m, 1m);
            _contrastValue = AddSliderRow(4, "Contrast", _contrast,
                0, 50m, 150m, 1m);
            _temperatureValue = AddSliderRow(5, "Temperature", _temperature,
                0, 3500m, 10000m, 100m);

            _gammaRed.Scroll += GammaSliderChanged;
            _gammaGreen.Scroll += GammaSliderChanged;
            _gammaBlue.Scroll += GammaSliderChanged;
            _brightness.Scroll += SettingsChanged;
            _contrast.Scroll += SettingsChanged;
            _temperature.Scroll += SettingsChanged;
            _gammaRedValue.ValueChanged += NumericValueChanged;
            _gammaGreenValue.ValueChanged += NumericValueChanged;
            _gammaBlueValue.ValueChanged += NumericValueChanged;
            _brightnessValue.ValueChanged += NumericValueChanged;
            _contrastValue.ValueChanged += NumericValueChanged;
            _temperatureValue.ValueChanged += NumericValueChanged;

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Width = 412;
            actions.AutoSize = true;
            actions.WrapContents = true;
            actions.Margin = new Padding(0, 12, 0, 4);
            Button neutral = NewButton("Neutral", NeutralClicked);
            Button restore = NewButton("Restore", RestoreClicked);
            Button fullscreen = NewButton("Fullscreen", FullscreenClicked);
            Button export = NewButton("Export", ExportClicked);
            _installButton = NewButton("Install", InstallClicked);
            actions.Controls.Add(neutral);
            actions.Controls.Add(restore);
            actions.Controls.Add(fullscreen);
            actions.Controls.Add(export);
            actions.Controls.Add(_installButton);
            leftFlow.Controls.Add(actions);

            _autostartCheck = new CheckBox();
            _autostartCheck.AutoSize = true;
            _autostartCheck.Checked = _appSettings.AutoStartLoader;
            _autostartCheck.Margin = new Padding(3, 8, 3, 8);
            _autostartCheck.CheckedChanged += AutostartChanged;
            Register("Autostart", _autostartCheck);
            leftFlow.Controls.Add(_autostartCheck);

            _status = new Label();
            _status.AutoSize = false;
            _status.Width = 405;
            _status.Height = 72;
            _status.ForeColor = Color.FromArgb(64, 70, 78);
            _status.Margin = new Padding(3, 8, 3, 3);
            leftFlow.Controls.Add(_status);

            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.Padding = new Padding(12, 0, 0, 0);
            root.Controls.Add(right, 1, 0);

            TableLayoutPanel calibrationLayout = new TableLayoutPanel();
            calibrationLayout.Dock = DockStyle.Fill;
            calibrationLayout.ColumnCount = 1;
            calibrationLayout.RowCount = 2;
            calibrationLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 54));
            calibrationLayout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));
            right.Controls.Add(calibrationLayout);

            Label hint = new Label();
            hint.Dock = DockStyle.Fill;
            hint.Font = new Font(Font.FontFamily, 10.0f);
            hint.ForeColor = Color.FromArgb(64, 70, 78);
            hint.TextAlign = ContentAlignment.MiddleLeft;
            Register("CalibrationHint", hint);
            calibrationLayout.Controls.Add(hint, 0, 0);

            _pattern = new TestPatternControl();
            _pattern.Dock = DockStyle.Fill;
            calibrationLayout.Controls.Add(_pattern, 0, 1);

            _hslControl = new HslOverlayControl();
            _hslControl.SettingsChanged += HslSettingsChanged;
            _hslTab.Controls.Add(_hslControl);
        }

        private void PopulateMonitors()
        {
            _monitors = MonitorService.EnumerateMonitors();
            _monitorCombo.Items.Clear();
            for (int i = 0; i < _monitors.Count; i++)
                _monitorCombo.Items.Add(_monitors[i]);
            if (_monitorCombo.Items.Count > 0)
                _monitorCombo.SelectedIndex = 0;
            else
                _status.Text = Localizer.Get("NoMonitors");
        }

        private void MonitorChanged(object sender, EventArgs e)
        {
            DisplayMonitor monitor = CurrentMonitor;
            if (monitor == null)
                return;

            MonitorSession session;
            if (!_sessions.TryGetValue(monitor.StableId, out session))
            {
                session = new MonitorSession();
                session.Monitor = monitor;
                session.Context = ProfileService.LoadContext(monitor);
                session.Settings = session.Context.SavedSettings.Clone();
                session.RestoreRamp = MonitorService.GetGammaRamp(monitor)
                    ?? session.Context.BaseRamp.Clone();
                MonitorSettingsRecord record = FindMonitorRecord(
                    monitor.StableId);
                session.HslSettings = record == null
                    ? HslBandSettings.CreateDefault()
                    : (record.HslOverlay ?? HslBandSettings.CreateDefault())
                        .Clone();
                // Never auto-reactivate settings written by the unsafe
                // fullscreen-window implementation.
                if (_appSettings.HslOverlaySafetyVersion < 2)
                    session.HslSettings.Enabled = false;
                _sessions[monitor.StableId] = session;
            }
            LoadSettingsIntoControls(session.Settings);
            _hslControl.LoadSettings(session.HslSettings);
            UpdateMonitorState(session);
            if (HslOverlayManager.LiveOverlayAvailable
                && !session.Monitor.IsHdr && session.HslSettings.Enabled)
                _hslManager.StartOrUpdate(session.Monitor,
                    session.HslSettings);
        }

        private void UpdateMonitorState(MonitorSession session)
        {
            bool editable = !session.Monitor.IsHdr;
            _slidersPanel.Enabled = editable;
            _linkCheck.Enabled = editable;
            _installButton.Enabled = editable;
            _hslControl.SetHdrBlocked(!editable);
            _hslControl.SetRuntimeUnavailable();
            if (!editable)
                _hslManager.Stop(session.Monitor.StableId);
            if (session.Monitor.IsHdr)
            {
                _status.ForeColor = Color.FromArgb(170, 70, 30);
                _status.Text = Localizer.Get("HdrBlocked");
            }
            else
            {
                _status.ForeColor = Color.FromArgb(64, 70, 78);
                string baseName = String.IsNullOrWhiteSpace(
                    session.Context.BaseProfilePath)
                    ? "sRGB"
                    : Path.GetFileName(session.Context.BaseProfilePath);
                _status.Text = String.Format(Localizer.Get("ProfileBase"), baseName);
            }
        }

        private void LoadSettingsIntoControls(GammaSettings settings)
        {
            _loading = true;
            try
            {
                _gammaRed.Value = ToTrack(settings.GammaRed, 100,
                    _gammaRed.Minimum, _gammaRed.Maximum);
                _gammaGreen.Value = ToTrack(settings.GammaGreen, 100,
                    _gammaGreen.Minimum, _gammaGreen.Maximum);
                _gammaBlue.Value = ToTrack(settings.GammaBlue, 100,
                    _gammaBlue.Minimum, _gammaBlue.Maximum);
                _brightness.Value = ToTrack(settings.Brightness, 100,
                    _brightness.Minimum, _brightness.Maximum);
                _contrast.Value = ToTrack(settings.Contrast, 100,
                    _contrast.Minimum, _contrast.Maximum);
                _temperature.Value = Math.Max(_temperature.Minimum,
                    Math.Min(_temperature.Maximum, settings.Temperature));
                _targetCombo.Text = settings.TargetGamma.ToString("0.0#",
                    CultureInfo.InvariantCulture);
                _linkCheck.Checked = settings.LinkChannels;
                _pattern.TargetGamma = settings.TargetGamma;
                UpdateValueLabels();
            }
            finally
            {
                _loading = false;
            }
        }

        private GammaSettings ReadControls()
        {
            GammaSettings settings = new GammaSettings();
            settings.GammaRed = _gammaRed.Value / 100.0;
            settings.GammaGreen = _gammaGreen.Value / 100.0;
            settings.GammaBlue = _gammaBlue.Value / 100.0;
            settings.Brightness = _brightness.Value / 100.0;
            settings.Contrast = _contrast.Value / 100.0;
            settings.Temperature = _temperature.Value;
            settings.TargetGamma = ReadTargetGamma(false);
            settings.LinkChannels = _linkCheck.Checked;
            return settings;
        }

        private double ReadTargetGamma(bool showError)
        {
            string normalized = _targetCombo.Text.Trim().Replace(',', '.');
            double value;
            if (!Double.TryParse(normalized, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value)
                || value < 1.0 || value > 3.0)
            {
                if (showError)
                    MessageBox.Show(this, Localizer.Get("InvalidTarget"),
                        Localizer.Get("Error"), MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                return 2.2;
            }
            return value;
        }

        private void GammaSliderChanged(object sender, EventArgs e)
        {
            if (!_loading && _linkCheck.Checked)
            {
                TrackBar changed = (TrackBar)sender;
                _loading = true;
                _gammaRed.Value = changed.Value;
                _gammaGreen.Value = changed.Value;
                _gammaBlue.Value = changed.Value;
                _loading = false;
            }
            SettingsChanged(sender, e);
        }

        private void LinkChanged(object sender, EventArgs e)
        {
            if (!_loading && _linkCheck.Checked)
            {
                _loading = true;
                _gammaGreen.Value = _gammaRed.Value;
                _gammaBlue.Value = _gammaRed.Value;
                _loading = false;
            }
            SettingsChanged(sender, e);
        }

        private void NumericValueChanged(object sender, EventArgs e)
        {
            if (_loading)
                return;
            _loading = true;
            try
            {
                if (sender == _gammaRedValue)
                    _gammaRed.Value = (int)Math.Round(_gammaRedValue.Value * 100m);
                else if (sender == _gammaGreenValue)
                    _gammaGreen.Value = (int)Math.Round(_gammaGreenValue.Value * 100m);
                else if (sender == _gammaBlueValue)
                    _gammaBlue.Value = (int)Math.Round(_gammaBlueValue.Value * 100m);
                else if (sender == _brightnessValue)
                    _brightness.Value = (int)_brightnessValue.Value;
                else if (sender == _contrastValue)
                    _contrast.Value = (int)_contrastValue.Value;
                else if (sender == _temperatureValue)
                    _temperature.Value = (int)_temperatureValue.Value;

                if (_linkCheck.Checked && (sender == _gammaRedValue
                    || sender == _gammaGreenValue || sender == _gammaBlueValue))
                {
                    int linkedValue = sender == _gammaRedValue
                        ? _gammaRed.Value
                        : (sender == _gammaGreenValue
                            ? _gammaGreen.Value : _gammaBlue.Value);
                    _gammaRed.Value = linkedValue;
                    _gammaGreen.Value = linkedValue;
                    _gammaBlue.Value = linkedValue;
                }
            }
            finally
            {
                _loading = false;
            }
            SettingsChanged(sender, e);
        }

        private void SettingsChanged(object sender, EventArgs e)
        {
            if (_loading)
                return;
            MonitorSession session = CurrentSession;
            if (session == null)
                return;
            session.Settings = ReadControls();
            _pattern.TargetGamma = session.Settings.TargetGamma;
            UpdateValueLabels();
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void PreviewTimerTick(object sender, EventArgs e)
        {
            _previewTimer.Stop();
            MonitorSession session = CurrentSession;
            if (session == null || session.Monitor.IsHdr)
                return;
            try
            {
                GammaRamp ramp = ProfileService.BuildRamp(session.Context,
                    session.Settings);
                bool success = MonitorService.SetGammaRamp(
                    session.Monitor, ramp, true);
                if (!success)
                {
                    _status.ForeColor = Color.FromArgb(170, 70, 30);
                    _status.Text = Localizer.Get("PreviewFailed");
                }
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        private void NeutralClicked(object sender, EventArgs e)
        {
            GammaSettings neutral = new GammaSettings();
            neutral.TargetGamma = ReadTargetGamma(false);
            neutral.LinkChannels = _linkCheck.Checked;
            LoadSettingsIntoControls(neutral);
            SettingsChanged(sender, e);
        }

        private void RestoreClicked(object sender, EventArgs e)
        {
            MonitorSession session = CurrentSession;
            if (session == null)
                return;
            session.Settings = session.Context.SavedSettings.Clone();
            LoadSettingsIntoControls(session.Settings);
            SettingsChanged(sender, e);
        }

        private void FullscreenClicked(object sender, EventArgs e)
        {
            DisplayMonitor monitor = CurrentMonitor;
            if (monitor == null)
                return;
            using (FullScreenTestForm form = new FullScreenTestForm(
                monitor, ReadTargetGamma(false)))
                form.ShowDialog(this);
        }

        private void ExportClicked(object sender, EventArgs e)
        {
            MonitorSession session = CurrentSession;
            if (session == null)
                return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = Localizer.Get("SaveProfile");
                dialog.Filter = "ICC/ICM profile (*.icm;*.icc)|*.icm;*.icc";
                dialog.DefaultExt = "icm";
                dialog.AddExtension = true;
                dialog.FileName = "WinGamma_"
                    + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".icm";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    ProfileService.Export(session.Context, session.Settings,
                        dialog.FileName);
                    _status.ForeColor = Color.FromArgb(30, 120, 65);
                    _status.Text = String.Format(Localizer.Get("Exported"),
                        dialog.FileName);
                }
                catch (Exception exception)
                {
                    ShowError(exception);
                }
            }
        }

        private void InstallClicked(object sender, EventArgs e)
        {
            MonitorSession session = CurrentSession;
            if (session == null || session.Monitor.IsHdr)
                return;
            Enabled = false;
            try
            {
                string path = ProfileService.InstallAndAssociate(session.Monitor,
                    session.Context, session.Settings);
                GammaRamp committed = ProfileService.BuildRamp(session.Context,
                    session.Settings);
                MonitorService.SetGammaRamp(session.Monitor, committed, false);
                session.RestoreRamp = committed.Clone();
                session.Context = ProfileService.LoadContext(session.Monitor);
                session.Context.SavedSettings = session.Settings.Clone();
                SaveMonitorRecord(session, path);

                if (!_autostartCheck.Checked)
                    _autostartCheck.Checked = true;
                _status.ForeColor = Color.FromArgb(30, 120, 65);
                _status.Text = String.Format(Localizer.Get("Installed"),
                    Path.GetFileName(path));
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
            finally
            {
                Enabled = true;
                Activate();
            }
        }

        private void SaveMonitorRecord(MonitorSession session, string path)
        {
            MonitorSettingsRecord record =
                FindMonitorRecord(session.Monitor.StableId);
            if (record == null)
            {
                record = new MonitorSettingsRecord();
                _appSettings.Monitors.Add(record);
            }
            record.MonitorId = session.Monitor.StableId;
            record.FriendlyName = session.Monitor.FriendlyName;
            record.InstalledProfilePath = path;
            record.Values = session.Settings.Clone();
            record.HslOverlay = (session.HslSettings
                ?? HslBandSettings.CreateDefault()).Clone();
            SettingsStore.Save(_appSettings);
        }

        private MonitorSettingsRecord FindMonitorRecord(string monitorId)
        {
            for (int i = 0; i < _appSettings.Monitors.Count; i++)
            {
                if (String.Equals(_appSettings.Monitors[i].MonitorId,
                    monitorId, StringComparison.OrdinalIgnoreCase))
                    return _appSettings.Monitors[i];
            }
            return null;
        }

        private void HslSettingsChanged(object sender, EventArgs e)
        {
            if (_loading)
                return;
            MonitorSession session = CurrentSession;
            if (session == null)
                return;
            session.HslSettings = _hslControl.ReadSettings();
            _appSettings.HslOverlaySafetyVersion = 2;

            MonitorSettingsRecord record =
                FindMonitorRecord(session.Monitor.StableId);
            if (record == null)
            {
                record = new MonitorSettingsRecord();
                record.MonitorId = session.Monitor.StableId;
                record.FriendlyName = session.Monitor.FriendlyName;
                record.Values = session.Settings.Clone();
                _appSettings.Monitors.Add(record);
            }
            record.HslOverlay = session.HslSettings.Clone();
            SettingsStore.Save(_appSettings);

            if (session.Monitor.IsHdr || !session.HslSettings.Enabled)
                _hslManager.Stop(session.Monitor.StableId);
            else if (HslOverlayManager.LiveOverlayAvailable)
                _hslManager.StartOrUpdate(session.Monitor,
                    session.HslSettings);
        }

        private void AutostartChanged(object sender, EventArgs e)
        {
            if (_loading)
                return;
            try
            {
                SettingsStore.SetAutostart(_autostartCheck.Checked);
                _appSettings.AutoStartLoader = _autostartCheck.Checked;
                SettingsStore.Save(_appSettings);
                if (_autostartCheck.Checked)
                {
                    ProcessStartInfo start = new ProcessStartInfo();
                    start.FileName = Application.ExecutablePath;
                    start.Arguments = "--loader";
                    start.UseShellExecute = true;
                    Process.Start(start);
                }
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        private void LanguageChanged(object sender, EventArgs e)
        {
            if (_loading)
                return;
            Localizer.Language = _languageCombo.SelectedIndex == 1 ? "en" : "uk";
            _appSettings.Language = Localizer.Language;
            SettingsStore.Save(_appSettings);
            ApplyLanguage();
            MonitorSession session = CurrentSession;
            if (session != null)
                UpdateMonitorState(session);
        }

        private void ApplyLanguage()
        {
            Text = Localizer.Get("AppTitle");
            foreach (KeyValuePair<string, Control> item in _localized)
                item.Value.Text = Localizer.Get(item.Key);
            _calibrationTab.Text = Localizer.Get("CalibrationTab");
            _hslTab.Text = Localizer.Get("HslTab");
            _hslControl.ApplyLanguage();
            _pattern.Invalidate();
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            _previewTimer.Stop();
            _hslManager.Dispose();
            foreach (MonitorSession session in _sessions.Values)
            {
                try
                {
                    if (!session.Monitor.IsHdr && session.RestoreRamp != null)
                        MonitorService.SetGammaRamp(session.Monitor,
                            session.RestoreRamp, false);
                }
                catch (Exception exception)
                {
                    SettingsStore.Log("Could not restore ramp: " + exception);
                }
            }
        }

        private void UpdateValueLabels()
        {
            bool previousLoading = _loading;
            _loading = true;
            try
            {
                _gammaRedValue.Value = _gammaRed.Value / 100m;
                _gammaGreenValue.Value = _gammaGreen.Value / 100m;
                _gammaBlueValue.Value = _gammaBlue.Value / 100m;
                _brightnessValue.Value = _brightness.Value;
                _contrastValue.Value = _contrast.Value;
                _temperatureValue.Value = _temperature.Value;
            }
            finally
            {
                _loading = previousLoading;
            }
        }

        private DisplayMonitor CurrentMonitor
        {
            get { return _monitorCombo.SelectedItem as DisplayMonitor; }
        }

        private MonitorSession CurrentSession
        {
            get
            {
                DisplayMonitor monitor = CurrentMonitor;
                if (monitor == null)
                    return null;
                MonitorSession session;
                return _sessions.TryGetValue(monitor.StableId, out session)
                    ? session : null;
            }
        }

        private TableLayoutPanel NewHeaderRow(string key, Control value)
        {
            TableLayoutPanel row = new TableLayoutPanel();
            row.Width = 412;
            row.Height = 38;
            row.ColumnCount = 2;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            Register(key, label);
            value.Dock = DockStyle.Fill;
            row.Controls.Add(label, 0, 0);
            row.Controls.Add(value, 1, 0);
            return row;
        }

        private NumericUpDown AddSliderRow(int row, string key, TrackBar track,
            int decimals, decimal minimum, decimal maximum, decimal increment)
        {
            _slidersPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            Register(key, label);
            NumericUpDown value = new NumericUpDown();
            value.Dock = DockStyle.Fill;
            value.TextAlign = HorizontalAlignment.Right;
            value.DecimalPlaces = decimals;
            value.Minimum = minimum;
            value.Maximum = maximum;
            value.Increment = increment;
            value.ThousandsSeparator = false;
            track.Dock = DockStyle.Fill;
            _slidersPanel.Controls.Add(label, 0, row);
            _slidersPanel.Controls.Add(track, 1, row);
            _slidersPanel.Controls.Add(value, 2, row);
            return value;
        }

        private ComboBox NewCombo(bool listOnly)
        {
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = listOnly
                ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDownList;
            combo.IntegralHeight = false;
            combo.Height = 28;
            return combo;
        }

        private TrackBar NewTrack(int minimum, int maximum, int value,
            int tickFrequency)
        {
            TrackBar track = new TrackBar();
            track.Minimum = minimum;
            track.Maximum = maximum;
            track.Value = value;
            track.TickFrequency = tickFrequency;
            track.SmallChange = Math.Max(1, tickFrequency / 5);
            track.LargeChange = Math.Max(1, tickFrequency);
            track.AutoSize = false;
            track.Height = 48;
            return track;
        }

        private Button NewButton(string key, EventHandler handler)
        {
            Button button = new Button();
            button.AutoSize = true;
            button.Height = 34;
            button.Padding = new Padding(7, 2, 7, 2);
            button.Click += handler;
            Register(key, button);
            return button;
        }

        private void Register(string key, Control control)
        {
            _localized[key] = control;
            control.Text = Localizer.Get(key);
        }

        private static int ToTrack(double value, int multiplier,
            int minimum, int maximum)
        {
            int integer = (int)Math.Round(value * multiplier);
            return Math.Max(minimum, Math.Min(maximum, integer));
        }

        private void ShowError(Exception exception)
        {
            SettingsStore.Log(exception.ToString());
            _status.ForeColor = Color.FromArgb(170, 40, 40);
            _status.Text = exception.Message;
            MessageBox.Show(this, exception.Message, Localizer.Get("Error"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
