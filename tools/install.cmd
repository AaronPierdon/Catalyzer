@echo off
rem Double-click: build Catalyzer and install it on top of the current copy (it restarts by itself).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 pause
