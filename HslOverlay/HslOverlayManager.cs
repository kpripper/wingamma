using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace WinGamma
{
    internal sealed class HslOverlayManager : IDisposable
    {
        private sealed class Session
        {
            public Thread Thread;
            public OverlayWindow Window;
            public bool StopRequested;
        }

        private readonly Dictionary<string, Session> _sessions =
            new Dictionary<string, Session>(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new object();
        private bool _disposed;

        public void StartOrUpdate(DisplayMonitor monitor,
            HslBandSettings settings)
        {
            if (_disposed)
                throw new ObjectDisposedException("HslOverlayManager");
            if (monitor == null || settings == null)
                return;
            if (!settings.Enabled || monitor.IsHdr)
            {
                Stop(monitor.StableId);
                return;
            }

            lock (_sync)
            {
                Session existing;
                if (_sessions.TryGetValue(monitor.StableId, out existing))
                {
                    if (!existing.StopRequested && existing.Window != null
                        && !existing.Window.IsDisposed)
                    {
                        existing.Window.UpdateSettings(settings);
                        return;
                    }
                    _sessions.Remove(monitor.StableId);
                }

                Session session = new Session();
                session.Thread = new Thread(delegate()
                {
                    try
                    {
                        OverlayWindow window =
                            new OverlayWindow(monitor, settings);
                        lock (_sync)
                        {
                            session.Window = window;
                            if (session.StopRequested)
                            {
                                window.Dispose();
                                return;
                            }
                        }
                        Application.Run(window);
                    }
                    catch (Exception exception)
                    {
                        SettingsStore.Log("HSL overlay thread failed: "
                            + exception);
                    }
                    finally
                    {
                        lock (_sync)
                        {
                            Session current;
                            if (_sessions.TryGetValue(monitor.StableId,
                                out current) && Object.ReferenceEquals(
                                    current, session))
                                _sessions.Remove(monitor.StableId);
                        }
                    }
                });
                session.Thread.IsBackground = true;
                session.Thread.Name = "WinGamma HSL " + monitor.DeviceName;
                session.Thread.SetApartmentState(ApartmentState.STA);
                _sessions[monitor.StableId] = session;
                session.Thread.Start();
            }
        }

        public void Stop(string monitorId)
        {
            if (String.IsNullOrWhiteSpace(monitorId))
                return;
            Session session;
            lock (_sync)
            {
                if (!_sessions.TryGetValue(monitorId, out session))
                    return;
                session.StopRequested = true;
            }
            if (session.Window != null)
                session.Window.RequestClose();
        }

        public void StopAll()
        {
            Session[] sessions;
            lock (_sync)
            {
                sessions = new Session[_sessions.Count];
                _sessions.Values.CopyTo(sessions, 0);
                for (int i = 0; i < sessions.Length; i++)
                    sessions[i].StopRequested = true;
            }
            for (int i = 0; i < sessions.Length; i++)
            {
                if (sessions[i].Window != null)
                    sessions[i].Window.RequestClose();
            }
        }

        public void StopExcept(ISet<string> activeMonitorIds)
        {
            string[] monitorIds;
            lock (_sync)
            {
                List<string> stopped = new List<string>();
                foreach (string monitorId in _sessions.Keys)
                {
                    if (activeMonitorIds == null
                        || !activeMonitorIds.Contains(monitorId))
                        stopped.Add(monitorId);
                }
                monitorIds = stopped.ToArray();
            }
            for (int i = 0; i < monitorIds.Length; i++)
                Stop(monitorIds[i]);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            StopAll();
        }
    }
}
