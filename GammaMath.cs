using System;

namespace WinGamma
{
    public static class GammaMath
    {
        public static GammaRamp Identity()
        {
            GammaRamp ramp = new GammaRamp();
            for (int i = 0; i < 256; i++)
            {
                ushort value = (ushort)(i * 257);
                ramp.Red[i] = value;
                ramp.Green[i] = value;
                ramp.Blue[i] = value;
            }
            return ramp;
        }

        public static GammaRamp CreateAdjustment(GammaSettings settings)
        {
            GammaRamp ramp = new GammaRamp();
            double[] temperature = TemperatureMultipliers(settings.Temperature);
            FillChannel(ramp.Red, settings.GammaRed, settings.Brightness,
                settings.Contrast, temperature[0]);
            FillChannel(ramp.Green, settings.GammaGreen, settings.Brightness,
                settings.Contrast, temperature[1]);
            FillChannel(ramp.Blue, settings.GammaBlue, settings.Brightness,
                settings.Contrast, temperature[2]);
            return ramp;
        }

        public static GammaRamp Compose(GammaRamp baseRamp, GammaRamp adjustment)
        {
            GammaRamp result = new GammaRamp();
            ComposeChannel(baseRamp.Red, adjustment.Red, result.Red);
            ComposeChannel(baseRamp.Green, adjustment.Green, result.Green);
            ComposeChannel(baseRamp.Blue, adjustment.Blue, result.Blue);
            return result;
        }

        public static bool IsMonotonic(GammaRamp ramp)
        {
            return IsChannelMonotonic(ramp.Red)
                && IsChannelMonotonic(ramp.Green)
                && IsChannelMonotonic(ramp.Blue);
        }

        public static int MaxDifference(GammaRamp left, GammaRamp right)
        {
            int maximum = 0;
            for (int i = 0; i < 256; i++)
            {
                maximum = Math.Max(maximum, Math.Abs((int)left.Red[i] - right.Red[i]));
                maximum = Math.Max(maximum, Math.Abs((int)left.Green[i] - right.Green[i]));
                maximum = Math.Max(maximum, Math.Abs((int)left.Blue[i] - right.Blue[i]));
            }
            return maximum;
        }

        private static void FillChannel(ushort[] output, double gamma, double brightness,
            double contrast, double temperatureMultiplier)
        {
            gamma = Clamp(gamma, 0.5, 2.5);
            brightness = Clamp(brightness, -0.2, 0.2);
            contrast = Clamp(contrast, 0.5, 1.5);
            double previous = 0.0;

            for (int i = 0; i < 256; i++)
            {
                double x = i / 255.0;
                double y = Math.Pow(x, 1.0 / gamma);
                y = ((y - 0.5) * contrast) + 0.5 + brightness;
                y *= temperatureMultiplier;
                y = Clamp(y, 0.0, 1.0);
                if (y < previous)
                    y = previous;
                output[i] = (ushort)Math.Round(y * 65535.0);
                previous = y;
            }
        }

        private static void ComposeChannel(ushort[] baseValues, ushort[] adjustment,
            ushort[] output)
        {
            for (int i = 0; i < 256; i++)
            {
                double position = adjustment[i] / 65535.0 * 255.0;
                int lower = (int)Math.Floor(position);
                int upper = Math.Min(255, lower + 1);
                double fraction = position - lower;
                output[i] = (ushort)Math.Round(baseValues[lower]
                    + ((baseValues[upper] - baseValues[lower]) * fraction));
            }
        }

        private static bool IsChannelMonotonic(ushort[] values)
        {
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < values[i - 1])
                    return false;
            }
            return true;
        }

        private static double[] TemperatureMultipliers(int kelvin)
        {
            double temperature = Clamp(kelvin, 3500, 10000) / 100.0;
            double red;
            double green;
            double blue;

            if (temperature <= 66.0)
            {
                red = 255.0;
                green = 99.4708025861 * Math.Log(temperature) - 161.1195681661;
                blue = temperature <= 19.0
                    ? 0.0
                    : 138.5177312231 * Math.Log(temperature - 10.0) - 305.0447927307;
            }
            else
            {
                red = 329.698727446 * Math.Pow(temperature - 60.0, -0.1332047592);
                green = 288.1221695283 * Math.Pow(temperature - 60.0, -0.0755148492);
                blue = 255.0;
            }

            red = Clamp(red, 0.0, 255.0);
            green = Clamp(green, 0.0, 255.0);
            blue = Clamp(blue, 0.0, 255.0);

            double[] neutral = RawTemperature(6500);
            double r = (red / 255.0) / neutral[0];
            double g = (green / 255.0) / neutral[1];
            double b = (blue / 255.0) / neutral[2];
            double maximum = Math.Max(1.0, Math.Max(r, Math.Max(g, b)));
            return new[] { r / maximum, g / maximum, b / maximum };
        }

        private static double[] RawTemperature(int kelvin)
        {
            double t = kelvin / 100.0;
            double r;
            double g;
            double b;
            if (t <= 66.0)
            {
                r = 255.0;
                g = 99.4708025861 * Math.Log(t) - 161.1195681661;
                b = t <= 19.0 ? 0.0 : 138.5177312231 * Math.Log(t - 10.0) - 305.0447927307;
            }
            else
            {
                r = 329.698727446 * Math.Pow(t - 60.0, -0.1332047592);
                g = 288.1221695283 * Math.Pow(t - 60.0, -0.0755148492);
                b = 255.0;
            }
            return new[]
            {
                Clamp(r, 0.0, 255.0) / 255.0,
                Clamp(g, 0.0, 255.0) / 255.0,
                Clamp(b, 0.0, 255.0) / 255.0
            };
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
