@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%Start-CARVES-Agent-Test.ps1"

if not exist "%PS_SCRIPT%" (
  echo CARVES Agent Trial launcher is missing Start-CARVES-Agent-Test.ps1.
  echo Re-download the CARVES folder or run: carves test demo
  echo.
  if not "%CARVES_AGENT_TEST_NO_PAUSE%"=="1" pause
  exit /b 1
)

where powershell.exe >nul 2>nul
if errorlevel 1 (
  echo CARVES Agent Trial launcher could not find Windows PowerShell.
  echo Install PowerShell or run from a terminal with: carves test demo
  echo.
  if not "%CARVES_AGENT_TEST_NO_PAUSE%"=="1" pause
  exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %*
exit /b %ERRORLEVEL%
