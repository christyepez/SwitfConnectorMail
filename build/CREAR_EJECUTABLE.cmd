@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Crear-EXE.ps1"
if errorlevel 1 (echo Error compilando. Revise salida anterior.& pause & exit /b 1)
echo.
echo Ejecutable disponible en ..\publicado\win-x64\SwiftHourlyWorker.exe
pause
