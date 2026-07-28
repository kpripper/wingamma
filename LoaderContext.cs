using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinGamma
{
    internal sealed class LoaderContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly HslOverlayManager _hslManager;
        private bool _disposed;

        public LoaderContext()
        {
            AppSettings settings = SettingsStore.Load();
            Localizer.Language = settings.Language;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(Localizer.Get("TrayOpen"), null, OpenEditor);
            menu.Items.Add(Localizer.Get("TrayReload"), null,
                delegate { ScheduleReload(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Localizer.Get("TrayDisable"), null, DisableAutostart);
            menu.Items.Add(Localizer.Get("TrayExit"), null, ExitLoader);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = Localizer.Get("LoaderTooltip");
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += OpenEditor;

            _timer = new System.Windows.Forms.Timer();
            _hslManager = new HslOverlayManager();
            _timer.Interval = 1200;
            _timer.Tick += ReloadTimerTick;

            SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
            SystemEvents.PowerModeChanged += PowerModeChanged;
            SystemEvents.SessionSwitch += SessionSwitch;
            ScheduleReload();
        }

        private void ScheduleReload()
        {
            _timer.Stop();
            _timer.Start();
        }

        private void ReloadTimerTick(object sender, EventArgs e)
        {
            _timer.Stop();
            if (IsEditorActive())
            {
                _hslManager.StopAll();
                _timer.Interval = 2000;
                _timer.Start();
                return;
            }

            _timer.Interval = 1200;
            try
            {
                AppSettings settings = SettingsStore.Load();
                HashSet<string> activeHsl = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (DisplayMonitor monitor in MonitorService.EnumerateMonitors())
                {
                    if (monitor.IsHdr)
                        continue;
                    string path = MonitorService.GetCurrentProfilePath(monitor);
                    if (!String.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        GammaRamp ramp = IccProfile.ReadVcgt(
                            File.ReadAllBytes(path));
                        if (ramp != null)
                            MonitorService.SetGammaRamp(monitor, ramp, false);
                    }

                    MonitorSettingsRecord record =
                        FindRecord(settings, monitor.StableId);
                    if (HslOverlayManager.LiveOverlayAvailable
                        && settings.HslOverlaySafetyVersion >= 2
                        && record != null && record.HslOverlay != null
                        && record.HslOverlay.Enabled)
                    {
                        activeHsl.Add(monitor.StableId);
                        _hslManager.StartOrUpdate(monitor,
                            record.HslOverlay);
                    }
                }
                _hslManager.StopExcept(activeHsl);
            }
            catch (Exception exception)
            {
                SettingsStore.Log("Loader reload failed: " + exception);
            }
        }

        private static MonitorSettingsRecord FindRecord(AppSettings settings,
            string monitorId)
        {
            for (int i = 0; i < settings.Monitors.Count; i++)
            {
                if (String.Equals(settings.Monitors[i].MonitorId, monitorId,
                    StringComparison.OrdinalIgnoreCase))
                    return settings.Monitors[i];
            }
            return null;
        }

        private static bool IsEditorActive()
        {
            try
            {
                using (Mutex mutex = Mutex.OpenExisting(@"Local\WinGamma.EditorActive"))
                {
                    bool acquired = false;
                    try
                    {
                        acquired = mutex.WaitOne(0);
                        return !acquired;
                    }
                    finally
                    {
                        if (acquired)
                            mutex.ReleaseMutex();
                    }
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
            catch (AbandonedMutexException)
            {
                return false;
            }
        }

        private void DisplaySettingsChanged(object sender, EventArgs e)
        {
            ScheduleReload();
        }

        private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume || e.Mode == PowerModes.StatusChange)
                ScheduleReload();
        }

        private void SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock
                || e.Reason == SessionSwitchReason.SessionLogon
                || e.Reason == SessionSwitchReason.ConsoleConnect)
                ScheduleReload();
        }

        private void OpenEditor(object sender, EventArgs e)
        {
            try
            {
                Process.Start(Application.ExecutablePath);
            }
            catch (Exception exception)
            {
                SettingsStore.Log("Could not open editor: " + exception);
            }
        }

        private void DisableAutostart(object sender, EventArgs e)
        {
            try
            {
                SettingsStore.SetAutostart(false);
                AppSettings settings = SettingsStore.Load();
                settings.AutoStartLoader = false;
                SettingsStore.Save(settings);
            }
            catch (Exception exception)
            {
                SettingsStore.Log("Could not disable autostart: " + exception);
            }
        }

        private void ExitLoader(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            if (!_disposed)
            {
                _disposed = true;
                SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
                SystemEvents.PowerModeChanged -= PowerModeChanged;
                SystemEvents.SessionSwitch -= SessionSwitch;
                _timer.Dispose();
                _hslManager.Dispose();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.ExitThreadCore();
        }
    }
}
