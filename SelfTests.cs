using System;
using System.Globalization;
using System.IO;
using System.Text;

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
