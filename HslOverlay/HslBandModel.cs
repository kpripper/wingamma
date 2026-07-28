using System;

namespace WinGamma
{
    [Serializable]
    public struct HslBand
    {
        public string Name { get; set; }
        public float CenterHueDeg { get; set; }
        public float WidthDeg { get; set; }
        public float HueShiftDeg { get; set; }
        public float SaturationScale { get; set; }
        public float LuminanceShift { get; set; }

        public HslBand(string name, float centerHueDeg, float widthDeg)
        {
            Name = name;
            CenterHueDeg = centerHueDeg;
            WidthDeg = widthDeg;
            HueShiftDeg = 0.0f;
            SaturationScale = 1.0f;
            LuminanceShift = 0.0f;
        }
    }

    [Serializable]
    public sealed class HslBandSettings
    {
        public bool Enabled { get; set; }
        public HslBand[] Bands { get; set; }

        public HslBandSettings()
        {
            Enabled = false;
            Bands = CreateBands();
        }

        public static HslBandSettings CreateDefault()
        {
            return new HslBandSettings();
        }

        public HslBandSettings Clone()
        {
            HslBandSettings copy = new HslBandSettings();
            copy.Enabled = Enabled;
            copy.Bands = Bands == null
                ? CreateBands()
                : (HslBand[])Bands.Clone();
            return copy;
        }

        public void EnsureValid()
        {
            if (Bands == null || Bands.Length != 8)
                Bands = CreateBands();

            for (int i = 0; i < Bands.Length; i++)
            {
                HslBand band = Bands[i];
                band.CenterHueDeg = HslBandMath.WrapHue(band.CenterHueDeg);
                band.WidthDeg = Clamp(band.WidthDeg, 15.0f, 180.0f);
                band.HueShiftDeg = Clamp(band.HueShiftDeg, -180.0f, 180.0f);
                band.SaturationScale = Clamp(band.SaturationScale, 0.0f, 2.0f);
                band.LuminanceShift = Clamp(band.LuminanceShift, -1.0f, 1.0f);
                Bands[i] = band;
            }
        }

        private static HslBand[] CreateBands()
        {
            return new[]
            {
                new HslBand("Reds", 0.0f, 75.0f),
                new HslBand("Oranges", 30.0f, 75.0f),
                new HslBand("Yellows", 60.0f, 75.0f),
                new HslBand("Greens", 120.0f, 90.0f),
                new HslBand("Aquas", 180.0f, 75.0f),
                new HslBand("Blues", 210.0f, 90.0f),
                new HslBand("Purples", 270.0f, 90.0f),
                new HslBand("Magentas", 330.0f, 90.0f)
            };
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    public struct HslBandAdjustment
    {
        public float HueShiftDeg;
        public float SaturationScale;
        public float LuminanceShift;
        public float TotalRawWeight;
    }
}
