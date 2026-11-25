@echo off
rem Double-click to build. Runs build.ps1 with the execution policy bypassed for this run only.
rem Extra arguments are passed through, e.g.:  build.cmd -SkipInstaller
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
echo.
pause
