@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0carves.ps1" %*
exit /b %ERRORLEVEL%
