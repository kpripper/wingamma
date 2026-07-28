using System;
using System.Collections.Generic;

namespace WinGamma
{
    internal static class Localizer
    {
        private static readonly Dictionary<string, string[]> Text =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "AppTitle", new[] { "WinGamma — калібрування дисплея", "WinGamma — display calibration" } },
            { "Monitor", new[] { "Монітор", "Monitor" } },
            { "Language", new[] { "Мова", "Language" } },
            { "TargetGamma", new[] { "Цільова гамма", "Target gamma" } },
            { "LinkRgb", new[] { "Зв’язати R/G/B", "Link R/G/B" } },
            { "GammaRed", new[] { "Гамма — червоний", "Gamma — red" } },
            { "GammaGreen", new[] { "Гамма — зелений", "Gamma — green" } },
            { "GammaBlue", new[] { "Гамма — синій", "Gamma — blue" } },
            { "Brightness", new[] { "Яскравість", "Brightness" } },
            { "Contrast", new[] { "Контраст", "Contrast" } },
            { "Temperature", new[] { "Температура", "Temperature" } },
            { "Neutral", new[] { "Нейтральні", "Neutral" } },
            { "Restore", new[] { "Відновити", "Restore" } },
            { "Fullscreen", new[] { "Повноекранний тест", "Full-screen test" } },
            { "Export", new[] { "Експортувати ICM", "Export ICM" } },
            { "Install", new[] { "Встановити й застосувати", "Install and apply" } },
            { "Autostart", new[] { "Автовідновлення після входу/сну", "Restore after sign-in/sleep" } },
            { "Ready", new[] { "Готово.", "Ready." } },
            { "HdrBlocked", new[] { "HDR/Advanced Color увімкнено. Вимкніть HDR для попереднього перегляду та встановлення SDR-профілю.", "HDR/Advanced Color is enabled. Turn HDR off to preview or install an SDR profile." } },
            { "PreviewFailed", new[] { "Драйвер не прийняв LUT або інша програма керує гамою.", "The driver rejected the LUT or another application controls gamma." } },
            { "Exported", new[] { "Профіль експортовано: {0}", "Profile exported: {0}" } },
            { "Installed", new[] { "Профіль встановлено й застосовано: {0}", "Profile installed and applied: {0}" } },
            { "InstallCancelled", new[] { "Встановлення скасовано або не вдалося.", "Installation was cancelled or failed." } },
            { "NoMonitors", new[] { "Активні монітори не знайдені.", "No active monitors were found." } },
            { "Error", new[] { "Помилка", "Error" } },
            { "SaveProfile", new[] { "Зберегти профіль кольору", "Save color profile" } },
            { "CalibrationHint", new[] { "Налаштовуйте повзунки, доки смугасті та суцільні поля здаються однаково яскравими.", "Adjust the sliders until striped and solid patches appear equally bright." } },
            { "TrayOpen", new[] { "Відкрити WinGamma", "Open WinGamma" } },
            { "TrayReload", new[] { "Застосувати профілі знову", "Reload profiles" } },
            { "TrayDisable", new[] { "Вимкнути автозапуск", "Disable autostart" } },
            { "TrayExit", new[] { "Вийти з loader", "Exit loader" } },
            { "LoaderTooltip", new[] { "WinGamma loader", "WinGamma loader" } },
            { "InvalidTarget", new[] { "Цільова гамма має бути від 1.0 до 3.0.", "Target gamma must be between 1.0 and 3.0." } },
            { "ProfileBase", new[] { "Базовий профіль: {0}", "Base profile: {0}" } },
            { "CalibrationTab", new[] { "Калібрування ICC", "ICC calibration" } },
            { "HslTab", new[] { "HSL Overlay", "HSL Overlay" } },
            { "HslEnable", new[] { "Увімкнути HSL-оверлей для вибраного монітора", "Enable HSL overlay for the selected monitor" } },
            { "HslReset", new[] { "Скинути смуги", "Reset bands" } },
            { "HslHint", new[] { "Окремий GPU-шар поверх ICC/vcgt. Корекція застосовується до кожного пікселя; в ICM вона не експортується.", "A separate GPU layer over ICC/vcgt. Per-pixel adjustments are not exported to ICM." } },
            { "HslHue", new[] { "Відтінок", "Hue" } },
            { "HslSat", new[] { "Насиченість", "Saturation" } },
            { "HslLum", new[] { "Яскравість", "Luminance" } },
            { "HslHdrBlocked", new[] { "HSL Overlay заблоковано, поки активний HDR.", "HSL Overlay is blocked while HDR is active." } }
        };

        private static string _language = "uk";

        public static string Language
        {
            get { return _language; }
            set { _language = String.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "uk"; }
        }

        public static string Get(string key)
        {
            string[] values;
            if (!Text.TryGetValue(key, out values))
                return key;
            return Language == "en" ? values[1] : values[0];
        }
    }
}
