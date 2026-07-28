# WinGamma

WinGamma is a small Windows 11 utility for visual display calibration. It offers
live SDR preview, per-monitor RGB gamma, brightness, contrast and color
temperature controls, test patterns, ICC/ICM export, profile installation and a
background calibration loader. Its optional HSL Overlay adds eight GPU-rendered
hue bands with independent hue, saturation and luminance/value controls.

The source is C# and can be edited in any text editor. Visual Studio is not
required. The HSL module uses the small Vortice.Windows Direct3D bindings rather
than maintaining unsafe handwritten COM vtables.

## Збірка

1. Скопіюйте каталог `wingamma` на комп’ютер із Windows 11.
2. Встановіть [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   (це компілятор і CLI, а не велика IDE).
3. Запустіть `build.bat` звичайним подвійним кліком або з `cmd.exe`.
4. Готові файли буде створено в каталозі `dist`.

`build.bat` виконує `dotnet restore` та `dotnet publish`. Для запуску
framework-dependent збірки потрібен .NET 8 Desktop Runtime. За потреби повністю
автономний пакет можна створити командою
`dotnet publish -c Release -r win-x64 --self-contained true`.

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
8. На вкладці `HSL Overlay` можна окремо ввімкнути GPU-оверлей і налаштувати
   Reds, Oranges, Yellows, Greens, Aquas, Blues, Purples та Magentas.

HSL-налаштування зберігаються у `%LOCALAPPDATA%\WinGamma\settings.xml`, але не
записуються в ICC/ICM: `vcgt` не вміє представляти корекцію за смугами hue.
Після оновлення зі збірки до safety hotfix старий прапорець увімкнення
ігнорується: оверлей потрібно явно ввімкнути знову у вкладці HSL.

Аварійне завершення оверлею з клавіатури:

```cmd
Win+R
taskkill /F /IM WinGamma.exe
```

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
WinGamma metadata round trips, HSL weight normalization and HSV/RGB math passed.
Hardware-dependent checks still need a real Windows 11 display and driver.

To empirically check whether Desktop Duplication sees the result before or
after the active `vcgt` LUT on a particular driver, run:

```bat
dist\WinGamma.exe --diagnose-layer-order
```

The test briefly applies a red-channel LUT, captures a controlled DDA frame and
always restores the original ramp in a `finally` block. Its result is
driver-specific and is saved as `layer-order-diagnostic.txt`.

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
  intentionally blocks live preview, installation and HSL Overlay while HDR is
  active. The first HSL implementation therefore handles only SDR BGRA8 frames.
- ICC/vcgt and HSL Overlay are independent layers. The former is a per-channel
  scan-out LUT; the latter captures the composed SDR desktop and performs an
  eight-band per-pixel HSV round trip in an HLSL pixel shader. The feature keeps
  the HSL name in the UI, while its current luminance control operates on HSV
  value.
- The click-through overlay is excluded from capture with
  `WDA_EXCLUDEFROMCAPTURE` to prevent feedback. `DXGI_ERROR_ACCESS_LOST` after
  UAC, lock/unlock or a mode switch triggers full capture-session recreation.
- USER32 input is disabled on the overlay HWND before rendering. WinGamma also
  verifies with `WindowFromPoint` that hit-testing resolves to an underlying
  window; if this safety check fails, the overlay closes without starting D3D.
- Secure desktop and protected/DRM content can appear black. Exclusive
  full-screen applications and software that reserves Desktop Duplication may
  prevent the overlay from working.
- Some graphics drivers apply one LUT to multiple outputs or let games and
  other calibration tools overwrite it. WinGamma verifies live preview when
  possible and the loader restores the installed profile after display events.
- Saturation needs channel mixing and is intentionally not implemented as a
  1D `vcgt` control.

Relevant Microsoft documentation:

- [Profile management functions](https://learn.microsoft.com/en-us/windows/win32/wcs/profile-management-functions)
- [SetDeviceGammaRamp limitations](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setdevicegammaramp)
- [Windows hardware display color calibration pipeline](https://learn.microsoft.com/en-us/windows/win32/wcs/display-calibration-mhc)
