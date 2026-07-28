using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinGamma
{
    public static class ProfileService
    {
        public static ProfileContext LoadContext(DisplayMonitor monitor)
        {
            string currentPath = MonitorService.GetCurrentProfilePath(monitor);
            byte[] currentBytes = ReadValidProfile(currentPath);
            WinGammaMetadata metadata = currentBytes == null
                ? null : IccProfile.ReadMetadata(currentBytes);

            string basePath = currentPath;
            byte[] baseBytes = currentBytes;
            GammaSettings settings = new GammaSettings();

            if (metadata != null && metadata.Settings != null)
            {
                settings = metadata.Settings.Clone();
                byte[] referenced = ReadValidProfile(metadata.BaseProfilePath);
                if (referenced != null)
                {
                    basePath = metadata.BaseProfilePath;
                    baseBytes = referenced;
                }
                else
                {
                    // The original profile disappeared. Treat the current profile
                    // as a new baseline and do not apply its correction twice.
                    settings = new GammaSettings();
                }
            }

            if (baseBytes == null)
            {
                basePath = EnsureGenericSrgbBase();
                baseBytes = File.ReadAllBytes(basePath);
            }

            GammaRamp baseRamp = IccProfile.ReadVcgt(baseBytes);
            if (baseRamp == null)
                baseRamp = GammaMath.Identity();

            ProfileContext context = new ProfileContext();
            context.CurrentProfilePath = currentPath;
            context.BaseProfilePath = basePath;
            context.BaseProfileBytes = baseBytes;
            context.BaseRamp = baseRamp;
            context.SavedSettings = settings;
            return context;
        }

        public static GammaRamp BuildRamp(ProfileContext context, GammaSettings settings)
        {
            return GammaMath.Compose(context.BaseRamp,
                GammaMath.CreateAdjustment(settings));
        }

        public static byte[] BuildProfile(ProfileContext context,
            GammaSettings settings)
        {
            GammaRamp ramp = BuildRamp(context, settings);
            WinGammaMetadata metadata = new WinGammaMetadata();
            metadata.Version = 1;
            metadata.BaseProfilePath = context.BaseProfilePath;
            metadata.Settings = settings.Clone();
            return IccProfile.CreateProfile(context.BaseProfileBytes, ramp, metadata);
        }

        public static void Export(ProfileContext context, GammaSettings settings,
            string destination)
        {
            byte[] profile = BuildProfile(context, settings);
            File.WriteAllBytes(destination, profile);
        }

        public static string InstallAndAssociate(DisplayMonitor monitor,
            ProfileContext context, GammaSettings settings)
        {
            if (monitor.IsHdr)
                throw new InvalidOperationException(Localizer.Get("HdrBlocked"));

            string directory = Path.Combine(SettingsStore.DataDirectory, "Profiles");
            Directory.CreateDirectory(directory);
            string safeName = MakeSafeName(monitor.FriendlyName);
            string filename = "WinGamma_" + safeName + "_"
                + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".icm";
            string stagingPath = Path.Combine(directory, filename);
            Export(context, settings, stagingPath);

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = System.Windows.Forms.Application.ExecutablePath;
            start.Arguments = "--install-profile " + SettingsStore.Quote(stagingPath);
            start.Verb = "runas";
            start.UseShellExecute = true;
            try
            {
                using (Process process = Process.Start(start))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new InvalidOperationException(
                            "InstallColorProfile failed with exit code "
                            + process.ExitCode + ".");
                }
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException(
                    Localizer.Get("InstallCancelled"), exception);
            }

            string profileName = Path.GetFileName(stagingPath);
            string[] deviceCandidates = new[]
            {
                monitor.StableId,
                monitor.DeviceName,
                monitor.FriendlyName
            };
            Exception lastError = null;
            for (int i = 0; i < deviceCandidates.Length; i++)
            {
                string device = deviceCandidates[i];
                if (String.IsNullOrWhiteSpace(device))
                    continue;
                try
                {
                    if (!NativeMethods.WcsSetUsePerUserProfiles(device,
                        NativeMethods.CLASS_MONITOR, true))
                        ThrowLastWin32("WcsSetUsePerUserProfiles");
                    if (!NativeMethods.WcsAssociateColorProfileWithDevice(
                        NativeMethods.WCS_PROFILE_MANAGEMENT_SCOPE_CURRENT_USER,
                        profileName, device))
                        ThrowLastWin32("WcsAssociateColorProfileWithDevice");
                    if (!NativeMethods.WcsSetDefaultColorProfile(
                        NativeMethods.WCS_PROFILE_MANAGEMENT_SCOPE_CURRENT_USER,
                        device, NativeMethods.CPT_ICC, NativeMethods.CPST_NONE,
                        0, profileName))
                        ThrowLastWin32("WcsSetDefaultColorProfile");
                    return stagingPath;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                    SettingsStore.Log("Profile association failed for '"
                        + device + "': " + exception.Message);
                }
            }
            throw new InvalidOperationException("Windows installed the profile but "
                + "could not associate it with the monitor.", lastError);
        }

        public static bool InstallElevated(string profilePath)
        {
            if (!File.Exists(profilePath) || !IccProfile.IsValid(File.ReadAllBytes(profilePath)))
                return false;
            return NativeMethods.InstallColorProfile(null,
                Path.GetFullPath(profilePath));
        }

        private static byte[] ReadValidProfile(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return null;
                byte[] bytes = File.ReadAllBytes(path);
                return IccProfile.IsValid(bytes) ? bytes : null;
            }
            catch
            {
                return null;
            }
        }

        private static string EnsureGenericSrgbBase()
        {
            string directory = Path.Combine(SettingsStore.DataDirectory,
                "BaseProfiles");
            string path = Path.Combine(directory, "WinGamma_sRGB_Base.icm");
            Directory.CreateDirectory(directory);
            if (!File.Exists(path)
                || !IccProfile.IsValid(File.ReadAllBytes(path)))
                File.WriteAllBytes(path, IccProfile.CreateGenericSrgbProfile());
            return path;
        }

        private static string MakeSafeName(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return "Display";
            StringBuilder result = new StringBuilder();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < value.Length && result.Length < 40; i++)
            {
                char character = value[i];
                if (Array.IndexOf(invalid, character) < 0
                    && !Char.IsControl(character))
                    result.Append(Char.IsWhiteSpace(character) ? '_' : character);
            }
            return result.Length == 0 ? "Display" : result.ToString();
        }

        private static void ThrowLastWin32(string operation)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, operation + " failed");
        }
    }
}
