@echo off
setlocal
cd /d "%~dp0"

call build.bat
if errorlevel 1 exit /b %errorlevel%

"dist\WinGamma.exe" --self-test
set "TEST_RESULT=%ERRORLEVEL%"
if exist "%LOCALAPPDATA%\WinGamma\self-test.txt" (
  type "%LOCALAPPDATA%\WinGamma\self-test.txt"
)

if not "%TEST_RESULT%"=="0" (
  echo Self-test failed with exit code %TEST_RESULT%.
  exit /b %TEST_RESULT%
)

echo All platform-independent self-tests passed.
echo.
echo Optional hardware diagnostic (temporarily changes and restores the LUT):
echo   dist\WinGamma.exe --diagnose-layer-order
echo It records whether this monitor driver exposes vcgt changes to DDA.
endlocal
