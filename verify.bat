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
endlocal
