@echo off
rem Double-click to build a release. Runs publish.ps1 even if PowerShell scripts are blocked on this PC.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1"
