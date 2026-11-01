@echo off
setlocal
rem Builds Release, streams the output to build-logs\build-<timestamp>.log, writes
rem build-logs\last-build.txt, and shows a result message box. After a failure the box offers to
rem open the log. Pass -NoPopup (or set CI) for unattended runs.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
exit /b %ERRORLEVEL%
