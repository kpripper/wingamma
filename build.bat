@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo ERROR: .NET Framework C# compiler was not found.
  echo Enable or install .NET Framework 4.8, then run this file again.
  exit /b 1
)

if not exist "dist" mkdir "dist"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 ^
  /out:"dist\WinGamma.exe" /win32manifest:"app.manifest" ^
  /reference:System.dll /reference:System.Core.dll ^
  /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  /reference:System.Xml.dll ^
  GammaMath.cs IccProfile.cs LoaderContext.cs Localizer.cs MainForm.cs ^
  Models.cs MonitorService.cs NativeMethods.cs ProfileService.cs Program.cs ^
  SelfTests.cs SettingsStore.cs TestPatternControl.cs

if errorlevel 1 exit /b %errorlevel%
copy /y "app.config" "dist\WinGamma.exe.config" >nul
echo Built: %CD%\dist\WinGamma.exe
endlocal
