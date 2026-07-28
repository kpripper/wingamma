using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace WinGamma
{
    internal static class SelfTests
    {
        private static int _passed;

        public static int Run()
        {
            StringBuilder report = new StringBuilder();
            try
            {
                Test(report, "Identity ramp", TestIdentity);
                Test(report, "Neutral adjustment", TestNeutralAdjustment);
                Test(report, "Extreme adjustment monotonicity",
                    TestExtremeAdjustment);
                Test(report, "ICC sRGB generation", TestGenericProfile);
                Test(report, "ICC vcgt round trip", TestVcgtRoundTrip);
                Test(report, "WinGamma metadata round trip",
                    TestMetadataRoundTrip);
                Test(report, "Invalid ICC rejection", TestInvalidProfile);
                Test(report, "HSL band normalization", TestHslBandNormalization);
                Test(report, "HSL neutral adjustment", TestHslNeutral);
                Test(report, "HSV RGB round trip", TestHsvRoundTrip);
                Test(report, "HSL settings XML round trip",
                    TestHslSettingsRoundTrip);
                report.AppendLine("PASS: " + _passed + " tests.");
                WriteReport(report.ToString());
                return 0;
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL: " + exception);
                WriteReport(report.ToString());
                return 3;
            }
        }

        private static void TestIdentity()
        {
            GammaRamp ramp = GammaMath.Identity();
            Assert(ramp.Red[0] == 0, "identity black");
            Assert(ramp.Red[255] == 65535, "identity white");
            Assert(ramp.Green[128] == 128 * 257, "identity midpoint");
            Assert(GammaMath.IsMonotonic(ramp), "identity monotonic");
        }

        private static void TestNeutralAdjustment()
        {
            GammaRamp identity = GammaMath.Identity();
            GammaRamp adjustment =
                GammaMath.CreateAdjustment(new GammaSettings());
            Assert(GammaMath.MaxDifference(identity, adjustment) <= 1,
                "neutral settings must produce identity");
            GammaRamp composed = GammaMath.Compose(identity, adjustment);
            Assert(GammaMath.MaxDifference(identity, composed) <= 1,
                "identity composition");
        }

        private static void TestExtremeAdjustment()
        {
            GammaSettings settings = new GammaSettings();
            settings.GammaRed = 0.5;
            settings.GammaGreen = 2.5;
            settings.GammaBlue = 1.7;
            settings.Brightness = 0.2;
            settings.Contrast = 1.5;
            settings.Temperature = 3500;
            GammaRamp ramp = GammaMath.CreateAdjustment(settings);
            Assert(GammaMath.IsMonotonic(ramp), "extreme ramp monotonic");
        }

        private static void TestGenericProfile()
        {
            byte[] profile = IccProfile.CreateGenericSrgbProfile();
            Assert(IccProfile.IsValid(profile), "generated ICC validity");
            Assert(profile.Length > 512, "generated ICC size");
            GammaRamp ramp = IccProfile.ReadVcgt(profile);
            Assert(ramp != null, "generic profile vcgt");
            Assert(GammaMath.MaxDifference(GammaMath.Identity(), ramp) == 0,
                "generic profile identity vcgt");
            bool nonZeroId = false;
            for (int i = 84; i < 100; i++)
                nonZeroId |= profile[i] != 0;
            Assert(nonZeroId, "profile ID");
        }

        private static void TestVcgtRoundTrip()
        {
            byte[] profile = IccProfile.CreateGenericSrgbProfile();
            GammaSettings settings = new GammaSettings();
            settings.GammaRed = 1.15;
            settings.GammaGreen = 1.07;
            settings.GammaBlue = 0.93;
            settings.Brightness = -0.03;
            GammaRamp expected = GammaMath.CreateAdjustment(settings);
            WinGammaMetadata metadata = new WinGammaMetadata();
            metadata.Version = 1;
            metadata.BaseProfilePath = @"C:\Color\Base Profile.icm";
            metadata.Settings = settings;
            byte[] modified = IccProfile.CreateProfile(profile, expected, metadata);
            GammaRamp actual = IccProfile.ReadVcgt(modified);
            Assert(actual != null, "round-trip vcgt exists");
            Assert(GammaMath.MaxDifference(expected, actual) == 0,
                "round-trip vcgt values");
            Assert(IccProfile.IsValid(modified), "modified ICC validity");
        }

        private static void TestMetadataRoundTrip()
        {
            byte[] profile = IccProfile.CreateGenericSrgbProfile();
            GammaSettings settings = new GammaSettings();
            settings.GammaRed = 1.23;
            settings.GammaGreen = 1.17;
            settings.GammaBlue = 0.91;
            settings.Brightness = 0.04;
            settings.Contrast = 1.12;
            settings.Temperature = 5700;
            settings.TargetGamma = 2.4;
            settings.LinkChannels = false;
            WinGammaMetadata metadata = new WinGammaMetadata();
            metadata.Version = 1;
            metadata.BaseProfilePath = @"C:\Кольори\Заводський.icm";
            metadata.Settings = settings;

            byte[] output = IccProfile.CreateProfile(profile,
                GammaMath.Identity(), metadata);
            WinGammaMetadata read = IccProfile.ReadMetadata(output);
            Assert(read != null, "metadata exists");
            Assert(read.BaseProfilePath == metadata.BaseProfilePath,
                "unicode base path");
            Assert(Math.Abs(read.Settings.GammaRed - 1.23) < 0.000001,
                "metadata gamma");
            Assert(read.Settings.Temperature == 5700, "metadata temperature");
            Assert(!read.Settings.LinkChannels, "metadata linked flag");
        }

        private static void TestInvalidProfile()
        {
            Assert(!IccProfile.IsValid(new byte[128]), "short ICC rejected");
            byte[] profile = IccProfile.CreateGenericSrgbProfile();
            profile[36] = (byte)'x';
            Assert(!IccProfile.IsValid(profile), "bad signature rejected");
        }

        private static void TestHslBandNormalization()
        {
            HslBandSettings settings = HslBandSettings.CreateDefault();
            for (int hue = 0; hue < 360; hue++)
            {
                float[] weights = HslBandMath.NormalizedWeights(
                    hue, settings);
                double sum = 0.0;
                for (int i = 0; i < weights.Length; i++)
                    sum += weights[i];
                Assert(Math.Abs(sum - 1.0) < 0.0001,
                    "normalized weights at hue " + hue);
            }
            Assert(Math.Abs(HslBandMath.AngleDiff(359.0f, 1.0f) + 2.0f)
                < 0.0001, "hue wrap");
        }

        private static void TestHslNeutral()
        {
            HslBandSettings settings = HslBandSettings.CreateDefault();
            for (int hue = 0; hue < 360; hue += 7)
            {
                HslBandAdjustment adjustment =
                    HslBandMath.Evaluate(hue, settings);
                Assert(Math.Abs(adjustment.HueShiftDeg) < 0.0001,
                    "neutral hue shift");
                Assert(Math.Abs(adjustment.SaturationScale - 1.0f) < 0.0001,
                    "neutral saturation scale");
                Assert(Math.Abs(adjustment.LuminanceShift) < 0.0001,
                    "neutral value shift");
            }
        }

        private static void TestHsvRoundTrip()
        {
            float[,] samples = {
                { 0.0f, 0.0f, 0.0f },
                { 1.0f, 1.0f, 1.0f },
                { 1.0f, 0.0f, 0.0f },
                { 0.12f, 0.53f, 0.91f },
                { 0.84f, 0.27f, 0.44f }
            };
            for (int i = 0; i < samples.GetLength(0); i++)
            {
                float h;
                float s;
                float v;
                HslBandMath.RgbToHsv(samples[i, 0], samples[i, 1],
                    samples[i, 2], out h, out s, out v);
                float r;
                float g;
                float b;
                HslBandMath.HsvToRgb(h, s, v, out r, out g, out b);
                Assert(Math.Abs(r - samples[i, 0]) < 0.0001, "round-trip R");
                Assert(Math.Abs(g - samples[i, 1]) < 0.0001, "round-trip G");
                Assert(Math.Abs(b - samples[i, 2]) < 0.0001, "round-trip B");
            }
        }

        private static void TestHslSettingsRoundTrip()
        {
            AppSettings settings = new AppSettings();
            MonitorSettingsRecord record = new MonitorSettingsRecord();
            record.MonitorId = "test-monitor";
            record.HslOverlay.Enabled = true;
            HslBand band = record.HslOverlay.Bands[3];
            band.HueShiftDeg = -17.0f;
            band.SaturationScale = 1.42f;
            band.LuminanceShift = 0.08f;
            record.HslOverlay.Bands[3] = band;
            settings.Monitors.Add(record);

            XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(stream, settings);
                stream.Position = 0;
                AppSettings read =
                    (AppSettings)serializer.Deserialize(stream);
                Assert(read.Monitors.Count == 1, "HSL monitor record");
                Assert(read.Monitors[0].HslOverlay.Enabled,
                    "HSL enabled flag");
                Assert(Math.Abs(read.Monitors[0].HslOverlay.Bands[3]
                    .SaturationScale - 1.42f) < 0.0001,
                    "HSL band value");
            }
        }

        private static void Test(StringBuilder report, string name, Action action)
        {
            action();
            _passed++;
            report.AppendLine("OK   " + name);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(
                    "Assertion failed: " + message);
        }

        private static void WriteReport(string report)
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.DataDirectory);
                string path = Path.Combine(SettingsStore.DataDirectory,
                    "self-test.txt");
                File.WriteAllText(path, report, Encoding.UTF8);
                SettingsStore.Log(report.Replace(Environment.NewLine, " | "));
            }
            catch
            {
            }
        }
    }
}
