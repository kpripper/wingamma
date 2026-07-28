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
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        private readonly DisplayMonitor _monitor;
        private readonly Timer _renderTimer;
        private readonly object _settingsLock = new object();
        private HslBandSettings _settings;
        private OverlayRenderer _renderer;
        private DateTime _retryAfterUtc;

        public OverlayWindow(DisplayMonitor monitor, HslBandSettings settings)
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
                // This is an opaque replacement frame. WS_EX_LAYERED is
                // intentionally omitted because flip-model swap chains do not
                // reliably present into layered HWNDs.
                value.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOPMOST
                    | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
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
                if (!NativeMethods.SetWindowDisplayAffinity(Handle,
                    NativeMethods.WDA_EXCLUDEFROMCAPTURE))
                {
                    throw new Win32Exception(
                        "Windows could not exclude the HSL overlay "
                        + "from capture.");
                }
                CreateRenderer();
                _renderTimer.Start();
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
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
        }
    }
}
