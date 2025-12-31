@echo off
setlocal

REM Runs the PowerShell vendor bootstrap in a predictable way
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0vendor-bootstrap.ps1"
if errorlevel 1 (
  echo.
  echo Vendor bootstrap FAILED.
  exit /b 1
)

echo.
echo Vendor bootstrap OK.
exit /b 0
