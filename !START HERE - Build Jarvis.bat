@echo off
rem Double-click this to build Jarvis on Windows 10 or 11. It uses the C# compiler that comes with Windows,
rem so nothing needs installing. The finished Jarvis.exe and Jarvis-Setup.exe land in the "out\windows" folder.
cd /d "%~dp0"
echo Building Jarvis...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ci\build-windows.ps1"
if errorlevel 1 (
  echo.
  echo The build failed. Scroll up to see why.
  pause
  exit /b 1
)
echo.
echo Done! Opening the folder with Jarvis.exe and Jarvis-Setup.exe...
start "" "%~dp0out\windows"
pause
