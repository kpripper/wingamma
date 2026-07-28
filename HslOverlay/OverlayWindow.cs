using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WinGamma
{
    internal sealed class OverlayWindow : Form
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_HOTKEY = 0x0312;
        private const int HTTRANSPARENT = -1;
        private const int EmergencyHotKeyId = 0x5747;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_F12 = 0x7B;
        private const uint LWA_ALPHA = 0x00000002;

        private readonly DisplayMonitor _monitor;
        private readonly Timer _renderTimer;
        private readonly Timer _safetyTimer;
        private readonly object _settingsLock = new object();
        private HslBandSettings _settings;
        private OverlayRenderer _renderer;
        private DateTime _retryAfterUtc;

        public OverlayWindow(DisplayMonitor monitor, HslBandSettings settings,
            int safetyTimeoutMilliseconds)
        {
            _monitor = monitor;
            _settings = settings.Clone();
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            Bounds = monitor.Bounds;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            _renderTimer = new Timer();
            _renderTimer.Interval = 1;
            _renderTimer.Tick += RenderTick;
            if (safetyTimeoutMilliseconds > 0)
            {
                _safetyTimer = new Timer();
                _safetyTimer.Interval = safetyTimeoutMilliseconds;
                _safetyTimer.Tick += delegate
                {
                    _safetyTimer.Stop();
                    Close();
                };
            }
            Shown += OverlayShown;
            FormClosed += OverlayClosed;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOPMOST
                    | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED;
                return value;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WM_NCHITTEST)
            {
                message.Result = new IntPtr(HTTRANSPARENT);
                return;
            }
            if (message.Msg == WM_HOTKEY
                && message.WParam.ToInt32() == EmergencyHotKeyId)
            {
                Close();
                return;
            }
            base.WndProc(ref message);
        }

        public void UpdateSettings(HslBandSettings settings)
        {
            lock (_settingsLock)
                _settings = settings.Clone();
        }

        public void RequestClose()
        {
            if (IsDisposed)
                return;
            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke((MethodInvoker)Close);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private void OverlayShown(object sender, EventArgs e)
        {
            try
            {
                if (!NativeMethods.SetLayeredWindowAttributes(Handle, 0,
                    255, LWA_ALPHA))
                    throw new Win32Exception(
                        "Could not initialize the layered overlay window.");

                if (!NativeMethods.RegisterHotKey(Handle, EmergencyHotKeyId,
                    MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_F12))
                    throw new Win32Exception(
                        "Could not register the emergency overlay hotkey.");

                if (!NativeMethods.SetWindowDisplayAffinity(Handle,
                    NativeMethods.WDA_EXCLUDEFROMCAPTURE))
                {
                    throw new Win32Exception(
                        "Windows could not exclude the HSL overlay "
                        + "from capture.");
                }
                CreateRenderer();
                _renderTimer.Start();
                if (_safetyTimer != null)
                    _safetyTimer.Start();
            }
            catch (Exception exception)
            {
                SettingsStore.Log("HSL overlay initialization failed: "
                    + exception);
                Close();
            }
        }

        private void CreateRenderer()
        {
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
            _renderer = new OverlayRenderer(Handle, _monitor);
        }

        private void RenderTick(object sender, EventArgs e)
        {
            if (DateTime.UtcNow < _retryAfterUtc)
                return;
            if (_renderer == null)
            {
                try
                {
                    CreateRenderer();
                }
                catch (Exception exception)
                {
                    SettingsStore.Log("HSL recreation delayed: " + exception);
                    _retryAfterUtc = DateTime.UtcNow.AddSeconds(2);
                    return;
                }
            }
            HslBandSettings settings;
            lock (_settingsLock)
                settings = _settings.Clone();
            try
            {
                _renderer.Render(settings);
            }
            catch (DesktopCaptureAccessLostException)
            {
                SettingsStore.Log("HSL capture access lost for "
                    + _monitor.DeviceName + "; recreating.");
                try
                {
                    CreateRenderer();
                }
                catch (Exception exception)
                {
                    SettingsStore.Log("HSL recreation delayed: " + exception);
                    _retryAfterUtc = DateTime.UtcNow.AddSeconds(2);
                }
            }
            catch (Exception exception)
            {
                SettingsStore.Log("HSL render failed: " + exception);
                _renderTimer.Stop();
                Close();
            }
        }

        private void OverlayClosed(object sender, FormClosedEventArgs e)
        {
            _renderTimer.Stop();
            _renderTimer.Dispose();
            NativeMethods.UnregisterHotKey(Handle, EmergencyHotKeyId);
            if (_safetyTimer != null)
            {
                _safetyTimer.Stop();
                _safetyTimer.Dispose();
            }
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
        }
    }
}
