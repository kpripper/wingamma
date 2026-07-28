@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: .NET 8 SDK was not found.
  echo Install the lightweight SDK from https://dotnet.microsoft.com/download/dotnet/8.0
  echo Visual Studio is not required.
  exit /b 1
)

if exist "dist" rmdir /s /q "dist"

dotnet restore "WinGamma.csproj"
if errorlevel 1 exit /b %errorlevel%

dotnet publish "WinGamma.csproj" -c Release -r win-x64 --self-contained false -o "dist"
if errorlevel 1 exit /b %errorlevel%

echo Built: %CD%\dist\WinGamma.exe
endlocal
