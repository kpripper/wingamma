using System;

namespace WinGamma
{
    public static class HslBandMath
    {
        public static float WrapHue(float hueDeg)
        {
            float wrapped = hueDeg % 360.0f;
            return wrapped < 0.0f ? wrapped + 360.0f : wrapped;
        }

        public static float AngleDiff(float leftDeg, float rightDeg)
        {
            float difference = WrapHue(leftDeg) - WrapHue(rightDeg);
            if (difference > 180.0f)
                difference -= 360.0f;
            else if (difference < -180.0f)
                difference += 360.0f;
            return difference;
        }

        public static float BandWeight(float pixelHueDeg, HslBand band)
        {
            float halfWidth = Math.Max(0.001f, band.WidthDeg * 0.5f);
            float distance = Math.Abs(AngleDiff(pixelHueDeg, band.CenterHueDeg));
            if (distance >= halfWidth)
                return 0.0f;
            return 0.5f * (1.0f
                + (float)Math.Cos(Math.PI * distance / halfWidth));
        }

        public static float[] NormalizedWeights(float pixelHueDeg,
            HslBandSettings settings)
        {
            if (settings == null)
                settings = HslBandSettings.CreateDefault();
            settings.EnsureValid();

            float[] weights = new float[8];
            float total = 0.0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = BandWeight(pixelHueDeg, settings.Bands[i]);
                total += weights[i];
            }

            if (total <= 0.000001f)
            {
                int nearest = 0;
                float nearestDistance = Single.MaxValue;
                for (int i = 0; i < settings.Bands.Length; i++)
                {
                    float distance = Math.Abs(AngleDiff(pixelHueDeg,
                        settings.Bands[i].CenterHueDeg));
                    if (distance < nearestDistance)
                    {
                        nearest = i;
                        nearestDistance = distance;
                    }
                }
                weights[nearest] = 1.0f;
                return weights;
            }

            for (int i = 0; i < weights.Length; i++)
                weights[i] /= total;
            return weights;
        }

        public static HslBandAdjustment Evaluate(float pixelHueDeg,
            HslBandSettings settings)
        {
            if (settings == null)
                settings = HslBandSettings.CreateDefault();
            settings.EnsureValid();

            float[] weights = NormalizedWeights(pixelHueDeg, settings);
            HslBandAdjustment result = new HslBandAdjustment();
            result.SaturationScale = 0.0f;

            for (int i = 0; i < weights.Length; i++)
            {
                HslBand band = settings.Bands[i];
                float rawWeight = BandWeight(pixelHueDeg, band);
                result.TotalRawWeight += rawWeight;
                result.HueShiftDeg += weights[i] * band.HueShiftDeg;
                result.SaturationScale += weights[i] * band.SaturationScale;
                result.LuminanceShift += weights[i] * band.LuminanceShift;
            }
            return result;
        }

        public static void AdjustHsv(ref float hueDeg, ref float saturation,
            ref float value, HslBandSettings settings)
        {
            if (settings == null || !settings.Enabled)
                return;
            HslBandAdjustment adjustment = Evaluate(hueDeg, settings);
            hueDeg = WrapHue(hueDeg + adjustment.HueShiftDeg);
            saturation = Clamp01(saturation * adjustment.SaturationScale);
            value = Clamp01(value + adjustment.LuminanceShift);
        }

        public static void RgbToHsv(float red, float green, float blue,
            out float hueDeg, out float saturation, out float value)
        {
            float maximum = Math.Max(red, Math.Max(green, blue));
            float minimum = Math.Min(red, Math.Min(green, blue));
            float delta = maximum - minimum;
            value = maximum;
            saturation = maximum <= 0.000001f ? 0.0f : delta / maximum;

            if (delta <= 0.000001f)
            {
                hueDeg = 0.0f;
                return;
            }
            if (maximum == red)
                hueDeg = 60.0f * (((green - blue) / delta) % 6.0f);
            else if (maximum == green)
                hueDeg = 60.0f * (((blue - red) / delta) + 2.0f);
            else
                hueDeg = 60.0f * (((red - green) / delta) + 4.0f);
            hueDeg = WrapHue(hueDeg);
        }

        public static void HsvToRgb(float hueDeg, float saturation, float value,
            out float red, out float green, out float blue)
        {
            float chroma = value * saturation;
            float sector = WrapHue(hueDeg) / 60.0f;
            float x = chroma * (1.0f - Math.Abs((sector % 2.0f) - 1.0f));
            float r1;
            float g1;
            float b1;
            if (sector < 1.0f) { r1 = chroma; g1 = x; b1 = 0.0f; }
            else if (sector < 2.0f) { r1 = x; g1 = chroma; b1 = 0.0f; }
            else if (sector < 3.0f) { r1 = 0.0f; g1 = chroma; b1 = x; }
            else if (sector < 4.0f) { r1 = 0.0f; g1 = x; b1 = chroma; }
            else if (sector < 5.0f) { r1 = x; g1 = 0.0f; b1 = chroma; }
            else { r1 = chroma; g1 = 0.0f; b1 = x; }
            float match = value - chroma;
            red = r1 + match;
            green = g1 + match;
            blue = b1 + match;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0.0f, Math.Min(1.0f, value));
        }
    }
}
