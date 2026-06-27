@echo off
setlocal

cd /d "%~dp0"

start "" "%~dp0bin\Debug\net8.0-windows\Iso11820Simulator.exe"
endlocal
