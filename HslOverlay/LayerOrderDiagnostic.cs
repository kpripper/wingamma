using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WinGamma
{
    internal static class LayerOrderDiagnostic
    {
        public static int Run()
        {
            DisplayMonitor monitor = null;
            foreach (DisplayMonitor candidate in MonitorService.EnumerateMonitors())
            {
                if (!candidate.IsHdr)
                {
                    monitor = candidate;
                    break;
                }
            }
            if (monitor == null)
            {
                MessageBox.Show("No SDR monitor is available. Turn HDR off "
                    + "and run the diagnostic again.", "WinGamma",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 4;
            }

            GammaRamp restore = MonitorService.GetGammaRamp(monitor);
            if (restore == null)
                throw new InvalidOperationException(
                    "Could not read the active gamma ramp.");

            Form pattern = new Form();
            pattern.FormBorderStyle = FormBorderStyle.None;
            pattern.StartPosition = FormStartPosition.Manual;
            pattern.Bounds = monitor.Bounds;
            pattern.BackColor = Color.FromArgb(128, 128, 128);
            pattern.TopMost = true;
            pattern.ShowInTaskbar = false;
            Panel marker = new Panel();
            marker.Size = new Size(12, 12);
            marker.Location = new Point(0, 0);
            pattern.Controls.Add(marker);

            Color before;
            Color after;
            bool applied = false;
            try
            {
                pattern.Show();
                pattern.Activate();
                Application.DoEvents();
                using (DesktopCapture capture = new DesktopCapture(monitor))
                {
                    before = CaptureChangedFrame(capture, marker, Color.Black);

                    GammaSettings adjustment = new GammaSettings();
                    adjustment.LinkChannels = false;
                    adjustment.GammaRed = 1.8;
                    GammaRamp diagnostic = GammaMath.Compose(restore,
                        GammaMath.CreateAdjustment(adjustment));
                    applied = MonitorService.SetGammaRamp(monitor,
                        diagnostic, false);
                    if (!applied)
                        throw new InvalidOperationException(
                            "The display driver rejected the diagnostic LUT.");
                    after = CaptureChangedFrame(capture, marker, Color.White);
                }
            }
            finally
            {
                if (applied)
                    MonitorService.SetGammaRamp(monitor, restore, false);
                pattern.Close();
                pattern.Dispose();
            }

            int redDelta = Math.Abs(after.R - before.R);
            int otherDelta = Math.Max(Math.Abs(after.G - before.G),
                Math.Abs(after.B - before.B));
            bool ddaContainsVcgt = redDelta > Math.Max(4, otherDelta + 3);
            string conclusion = ddaContainsVcgt
                ? "DDA appears to include the vcgt/gamma-ramp result."
                : "DDA appears to capture before vcgt/gamma-ramp.";
            StringBuilder report = new StringBuilder();
            report.AppendLine(DateTime.UtcNow.ToString("O"));
            report.AppendLine("Monitor: " + monitor.DeviceName + " / "
                + monitor.FriendlyName);
            report.AppendLine("Before BGRA sample: R=" + before.R + " G="
                + before.G + " B=" + before.B);
            report.AppendLine("After BGRA sample: R=" + after.R + " G="
                + after.G + " B=" + after.B);
            report.AppendLine("Result: " + conclusion);
            report.AppendLine("This is an empirical driver-specific result, "
                + "not a Windows-wide guarantee.");
            Directory.CreateDirectory(SettingsStore.DataDirectory);
            string path = Path.Combine(SettingsStore.DataDirectory,
                "layer-order-diagnostic.txt");
            File.WriteAllText(path, report.ToString(), Encoding.UTF8);
            MessageBox.Show(report.ToString() + Environment.NewLine
                + "Saved to: " + path, "WinGamma layer-order diagnostic",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        private static Color CaptureChangedFrame(DesktopCapture capture,
            Panel marker, Color markerColor)
        {
            marker.BackColor = markerColor;
            marker.Invalidate();
            Application.DoEvents();
            Thread.Sleep(80);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Color? color = capture.CaptureAverageColor(250);
                if (color.HasValue)
                    return color.Value;
                marker.Visible = !marker.Visible;
                Application.DoEvents();
            }
            throw new TimeoutException(
                "Desktop Duplication did not provide a changed frame.");
        }
    }
}
