using System;
using System.Collections.Generic;
using System.Drawing;

namespace WinGamma
{
    [Serializable]
    public sealed class GammaSettings
    {
        public double GammaRed { get; set; }
        public double GammaGreen { get; set; }
        public double GammaBlue { get; set; }
        public double Brightness { get; set; }
        public double Contrast { get; set; }
        public int Temperature { get; set; }
        public double TargetGamma { get; set; }
        public bool LinkChannels { get; set; }

        public GammaSettings()
        {
            GammaRed = 1.0;
            GammaGreen = 1.0;
            GammaBlue = 1.0;
            Brightness = 0.0;
            Contrast = 1.0;
            Temperature = 6500;
            TargetGamma = 2.2;
            LinkChannels = true;
        }

        public GammaSettings Clone()
        {
            return (GammaSettings)MemberwiseClone();
        }

        public bool IsNeutral()
        {
            return Math.Abs(GammaRed - 1.0) < 0.0001
                && Math.Abs(GammaGreen - 1.0) < 0.0001
                && Math.Abs(GammaBlue - 1.0) < 0.0001
                && Math.Abs(Brightness) < 0.0001
                && Math.Abs(Contrast - 1.0) < 0.0001
                && Temperature == 6500;
        }
    }

    [Serializable]
    public sealed class MonitorSettingsRecord
    {
        public string MonitorId { get; set; }
        public string FriendlyName { get; set; }
        public string InstalledProfilePath { get; set; }
        public GammaSettings Values { get; set; }
        public HslBandSettings HslOverlay { get; set; }

        public MonitorSettingsRecord()
        {
            Values = new GammaSettings();
            HslOverlay = HslBandSettings.CreateDefault();
        }
    }

    [Serializable]
    public sealed class AppSettings
    {
        public string Language { get; set; }
        public bool AutoStartLoader { get; set; }
        public int HslOverlaySafetyVersion { get; set; }
        public bool HslClickThroughValidated { get; set; }
        public List<MonitorSettingsRecord> Monitors { get; set; }

        public AppSettings()
        {
            Language = "uk";
            AutoStartLoader = false;
            HslOverlaySafetyVersion = 0;
            HslClickThroughValidated = false;
            Monitors = new List<MonitorSettingsRecord>();
        }
    }

    public sealed class DisplayMonitor
    {
        public IntPtr Handle { get; set; }
        public string DeviceName { get; set; }
        public string FriendlyName { get; set; }
        public string StableId { get; set; }
        public Rectangle Bounds { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsHdr { get; set; }

        public override string ToString()
        {
            string primary = IsPrimary ? " • Primary" : String.Empty;
            string hdr = IsHdr ? " • HDR" : String.Empty;
            return FriendlyName + primary + hdr;
        }
    }

    public sealed class GammaRamp
    {
        public ushort[] Red { get; private set; }
        public ushort[] Green { get; private set; }
        public ushort[] Blue { get; private set; }

        public GammaRamp()
        {
            Red = new ushort[256];
            Green = new ushort[256];
            Blue = new ushort[256];
        }

        public GammaRamp Clone()
        {
            GammaRamp copy = new GammaRamp();
            Array.Copy(Red, copy.Red, 256);
            Array.Copy(Green, copy.Green, 256);
            Array.Copy(Blue, copy.Blue, 256);
            return copy;
        }
    }

    public sealed class ProfileContext
    {
        public string CurrentProfilePath { get; set; }
        public string BaseProfilePath { get; set; }
        public byte[] BaseProfileBytes { get; set; }
        public GammaRamp BaseRamp { get; set; }
        public GammaSettings SavedSettings { get; set; }
    }

    public sealed class WinGammaMetadata
    {
        public int Version { get; set; }
        public string BaseProfilePath { get; set; }
        public GammaSettings Settings { get; set; }
    }
}
