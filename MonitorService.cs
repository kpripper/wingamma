using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinGamma
{
    public static class MonitorService
    {
        private sealed class DisplayConfigInfo
        {
            public string FriendlyName;
            public string DevicePath;
            public bool IsHdr;
        }

        public static List<DisplayMonitor> EnumerateMonitors()
        {
            Dictionary<string, DisplayConfigInfo> config = ReadDisplayConfiguration();
            List<DisplayMonitor> monitors = new List<DisplayMonitor>();

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate(IntPtr handle, IntPtr hdc, ref RECT bounds, IntPtr data)
                {
                    MONITORINFOEX info = new MONITORINFOEX();
                    info.Size = (uint)Marshal.SizeOf(typeof(MONITORINFOEX));
                    if (!NativeMethods.GetMonitorInfo(handle, ref info))
                        return true;

                    string friendly = info.DeviceName;
                    string stableId = info.DeviceName;
                    DisplayConfigInfo displayConfig;
                    if (config.TryGetValue(info.DeviceName, out displayConfig))
                    {
                        if (!String.IsNullOrWhiteSpace(displayConfig.FriendlyName))
                            friendly = displayConfig.FriendlyName;
                        if (!String.IsNullOrWhiteSpace(displayConfig.DevicePath))
                            stableId = displayConfig.DevicePath;
                    }
                    else
                    {
                        DISPLAY_DEVICE device = new DISPLAY_DEVICE();
                        device.Size = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
                        if (NativeMethods.EnumDisplayDevices(info.DeviceName, 0, ref device, 0))
                        {
                            if (!String.IsNullOrWhiteSpace(device.DeviceString))
                                friendly = device.DeviceString;
                            if (!String.IsNullOrWhiteSpace(device.DeviceID))
                                stableId = device.DeviceID;
                        }
                    }

                    DisplayMonitor monitor = new DisplayMonitor();
                    monitor.Handle = handle;
                    monitor.DeviceName = info.DeviceName;
                    monitor.FriendlyName = friendly;
                    monitor.StableId = stableId;
                    monitor.Bounds = Rectangle.FromLTRB(bounds.Left, bounds.Top,
                        bounds.Right, bounds.Bottom);
                    monitor.IsPrimary =
                        (info.Flags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                    monitor.IsHdr = displayConfig != null && displayConfig.IsHdr;
                    monitors.Add(monitor);
                    return true;
                }, IntPtr.Zero);

            monitors.Sort(delegate(DisplayMonitor left, DisplayMonitor right)
            {
                if (left.IsPrimary != right.IsPrimary)
                    return left.IsPrimary ? -1 : 1;
                return String.Compare(left.FriendlyName, right.FriendlyName,
                    StringComparison.CurrentCultureIgnoreCase);
            });
            return monitors;
        }

        public static GammaRamp GetGammaRamp(DisplayMonitor monitor)
        {
            if (monitor == null)
                return null;
            IntPtr hdc = NativeMethods.CreateDC("DISPLAY", monitor.DeviceName,
                null, IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                return null;
            IntPtr buffer = Marshal.AllocHGlobal(3 * 256 * 2);
            try
            {
                if (!NativeMethods.GetDeviceGammaRamp(hdc, buffer))
                    return null;
                byte[] raw = new byte[3 * 256 * 2];
                Marshal.Copy(buffer, raw, 0, raw.Length);
                return FromNativeBytes(raw);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                NativeMethods.DeleteDC(hdc);
            }
        }

        public static bool SetGammaRamp(DisplayMonitor monitor, GammaRamp ramp,
            bool verify)
        {
            if (monitor == null || ramp == null || monitor.IsHdr)
                return false;
            IntPtr hdc = NativeMethods.CreateDC("DISPLAY", monitor.DeviceName,
                null, IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                return false;
            byte[] raw = ToNativeBytes(ramp);
            IntPtr buffer = Marshal.AllocHGlobal(raw.Length);
            try
            {
                Marshal.Copy(raw, 0, buffer, raw.Length);
                if (!NativeMethods.SetDeviceGammaRamp(hdc, buffer))
                    return false;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                NativeMethods.DeleteDC(hdc);
            }

            if (!verify)
                return true;
            GammaRamp actual = GetGammaRamp(monitor);
            return actual != null && GammaMath.MaxDifference(ramp, actual) <= 1024;
        }

        public static string GetCurrentProfilePath(DisplayMonitor monitor)
        {
            if (monitor == null)
                return null;
            IntPtr hdc = NativeMethods.CreateDC("DISPLAY", monitor.DeviceName,
                null, IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                return null;
            try
            {
                uint size = 512;
                StringBuilder path = new StringBuilder((int)size);
                if (!NativeMethods.GetICMProfile(hdc, ref size, path))
                {
                    if (size < 2 || size > 32768)
                        return null;
                    path = new StringBuilder((int)size);
                    if (!NativeMethods.GetICMProfile(hdc, ref size, path))
                        return null;
                }
                string result = path.ToString();
                return File.Exists(result) ? result : null;
            }
            finally
            {
                NativeMethods.DeleteDC(hdc);
            }
        }

        private static Dictionary<string, DisplayConfigInfo> ReadDisplayConfiguration()
        {
            Dictionary<string, DisplayConfigInfo> result =
                new Dictionary<string, DisplayConfigInfo>(StringComparer.OrdinalIgnoreCase);
            try
            {
                uint pathCount;
                uint modeCount;
                if (NativeMethods.GetDisplayConfigBufferSizes(
                    NativeMethods.QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount)
                    != NativeMethods.ERROR_SUCCESS)
                    return result;

                DISPLAYCONFIG_PATH_INFO[] paths =
                    new DISPLAYCONFIG_PATH_INFO[pathCount];
                DISPLAYCONFIG_MODE_INFO[] modes =
                    new DISPLAYCONFIG_MODE_INFO[modeCount];
                if (NativeMethods.QueryDisplayConfig(NativeMethods.QDC_ONLY_ACTIVE_PATHS,
                    ref pathCount, paths, ref modeCount, modes, IntPtr.Zero)
                    != NativeMethods.ERROR_SUCCESS)
                    return result;

                for (int i = 0; i < pathCount; i++)
                {
                    DISPLAYCONFIG_SOURCE_DEVICE_NAME source =
                        new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    source.Header.Type =
                        NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    source.Header.Size =
                        (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
                    source.Header.AdapterId = paths[i].SourceInfo.AdapterId;
                    source.Header.Id = paths[i].SourceInfo.Id;
                    if (NativeMethods.DisplayConfigGetSourceDeviceName(ref source)
                        != NativeMethods.ERROR_SUCCESS
                        || String.IsNullOrWhiteSpace(source.ViewGdiDeviceName))
                        continue;

                    DISPLAYCONFIG_TARGET_DEVICE_NAME target =
                        new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    target.Header.Type =
                        NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    target.Header.Size =
                        (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
                    target.Header.AdapterId = paths[i].TargetInfo.AdapterId;
                    target.Header.Id = paths[i].TargetInfo.Id;

                    DisplayConfigInfo info = new DisplayConfigInfo();
                    if (NativeMethods.DisplayConfigGetTargetDeviceName(ref target)
                        == NativeMethods.ERROR_SUCCESS)
                    {
                        info.FriendlyName = target.MonitorFriendlyDeviceName;
                        info.DevicePath = target.MonitorDevicePath;
                    }

                    DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO advanced =
                        new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                    advanced.Header.Type =
                        NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
                    advanced.Header.Size =
                        (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
                    advanced.Header.AdapterId = paths[i].TargetInfo.AdapterId;
                    advanced.Header.Id = paths[i].TargetInfo.Id;
                    if (NativeMethods.DisplayConfigGetAdvancedColorInfo(ref advanced)
                        == NativeMethods.ERROR_SUCCESS)
                    {
                        info.IsHdr = (advanced.Value & 0x2) != 0;
                    }
                    result[source.ViewGdiDeviceName] = info;
                }
            }
            catch
            {
                // Older drivers can reject DisplayConfig queries. SDR remains the safe fallback.
            }
            return result;
        }

        private static byte[] ToNativeBytes(GammaRamp ramp)
        {
            byte[] raw = new byte[3 * 256 * 2];
            Buffer.BlockCopy(ramp.Red, 0, raw, 0, 512);
            Buffer.BlockCopy(ramp.Green, 0, raw, 512, 512);
            Buffer.BlockCopy(ramp.Blue, 0, raw, 1024, 512);
            return raw;
        }

        private static GammaRamp FromNativeBytes(byte[] raw)
        {
            GammaRamp ramp = new GammaRamp();
            Buffer.BlockCopy(raw, 0, ramp.Red, 0, 512);
            Buffer.BlockCopy(raw, 512, ramp.Green, 0, 512);
            Buffer.BlockCopy(raw, 1024, ramp.Blue, 0, 512);
            return ramp;
        }
    }
}
