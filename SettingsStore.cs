using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace WinGamma
{
    public static class SettingsStore
    {
        private const string RunKey =
            @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "WinGammaLoader";

        public static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinGamma");

        public static readonly string SettingsPath =
            Path.Combine(DataDirectory, "settings.xml");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new AppSettings();
                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                using (FileStream stream = File.OpenRead(SettingsPath))
                    return (AppSettings)serializer.Deserialize(stream);
            }
            catch (Exception exception)
            {
                Log("Settings load failed: " + exception);
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(DataDirectory);
            string temporary = SettingsPath + ".tmp";
            XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
            using (FileStream stream = File.Create(temporary))
                serializer.Serialize(stream, settings);
            if (File.Exists(SettingsPath))
                File.Delete(SettingsPath);
            File.Move(temporary, SettingsPath);
        }

        public static void SetAutostart(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                {
                    string executable = System.Windows.Forms.Application.ExecutablePath;
                    key.SetValue(RunValue, Quote(executable) + " --loader",
                        RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(RunValue, false);
                }
            }
        }

        public static void Log(string message)
        {
            try
            {
                string directory = Path.Combine(DataDirectory, "Logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory,
                    DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
                string line = DateTime.UtcNow.ToString("O") + "  " + message
                    + Environment.NewLine;
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch
            {
                // Logging must never interrupt calibration or profile recovery.
            }
        }

        public static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
