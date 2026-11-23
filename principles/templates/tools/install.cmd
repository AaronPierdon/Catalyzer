@echo off
rem Double-click: runs tools\install.ps1 (a chat uses the Catalyzer link instead)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 pause
