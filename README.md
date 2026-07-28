# WinGamma

WinGamma is a small Windows 11 utility for visual display calibration. It offers
live SDR preview, per-monitor RGB gamma, brightness, contrast and color
temperature controls, test patterns, ICC/ICM export, profile installation and a
background calibration loader.

The application has no network functionality, NuGet packages or third-party
runtime dependencies. It targets the .NET Framework 4.8 included with Windows
11.

## Збірка

1. Скопіюйте каталог `wingamma` на комп’ютер із Windows 11.
2. Запустіть `build.bat` звичайним подвійним кліком або з `cmd.exe`.
3. Готовий файл буде створено як `dist\WinGamma.exe`.

Visual Studio не потрібна. Скрипт використовує системний компілятор:

```text
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

Якщо компілятор відсутній, увімкніть або встановіть .NET Framework 4.8 у
Windows Features і повторіть збірку. Файл `WinGamma.csproj` додано для тих, хто
все ж хоче користуватися MSBuild або легким редактором із підтримкою C#.

## Використання

1. Вимкніть HDR для монітора, який потрібно налаштувати.
2. Запустіть `WinGamma.exe` без прав адміністратора.
3. Виберіть монітор та цільову гамму. Типове значення — 2.2.
4. Налаштуйте R/G/B, яскравість, контраст і температуру. Порівнюйте смугасті
   та суцільні тестові поля; вони мають здаватися однаково яскравими.
5. `Експортувати ICM` лише записує файл.
6. `Встановити й застосувати` попросить UAC, встановить профіль у Windows та
   призначить його вибраному монітору для поточного користувача.
7. Увімкніть автовідновлення, щоб loader повторно застосовував `vcgt` після
   входу, сну, зміни режиму або перепідключення дисплея.

Поки зміни не встановлені, закриття редактора відновлює LUT, який був активним
на момент запуску. Після успішного встановлення відновлюється вже новий профіль.

## English quick start

Run `build.bat`, then open `dist\WinGamma.exe`. Select a monitor, keep the
default target gamma of 2.2 or enter a value from 1.0 to 3.0, and adjust the
controls while comparing the striped and solid patches. Export writes an
ICC/ICM file without changing Windows. Install requests UAC, installs the
profile and associates it with the selected monitor.

## Self-test

Run the included verification script:

```bat
verify.bat
```

Exit code `0` means that LUT generation, ICC structure, `vcgt`, profile ID and
WinGamma metadata round trips passed. Hardware-dependent checks still need a
real Windows 11 display and driver.

## Files and privacy

User settings, generated profiles and logs are stored under:

```text
%LOCALAPPDATA%\WinGamma
```

Autostart uses only:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\WinGammaLoader
```

WinGamma does not collect telemetry or send any data.

## Technical notes and limitations

- ICC and ICM are accepted as equivalent profile file extensions.
- The generated `vcgt` contains three channels, 256 entries per channel and
  16-bit values. Existing monitor profile tags are retained.
- Visual calibration cannot replace a colorimeter and does not guarantee
  measurement-grade color accuracy.
- Legacy gamma ramps have undefined behavior in HDR/Advanced Color. WinGamma
  intentionally blocks live preview and installation while HDR is active.
- Some graphics drivers apply one LUT to multiple outputs or let games and
  other calibration tools overwrite it. WinGamma verifies live preview when
  possible and the loader restores the installed profile after display events.
- Saturation needs channel mixing and is intentionally not implemented as a
  1D `vcgt` control.

Relevant Microsoft documentation:

- [Profile management functions](https://learn.microsoft.com/en-us/windows/win32/wcs/profile-management-functions)
- [SetDeviceGammaRamp limitations](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setdevicegammaramp)
- [Windows hardware display color calibration pipeline](https://learn.microsoft.com/en-us/windows/win32/wcs/display-calibration-mhc)
