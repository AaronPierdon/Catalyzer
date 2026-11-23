@echo off
rem Double-click: runs tools\build.ps1 (a chat uses the Catalyzer link instead)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if errorlevel 1 pause
