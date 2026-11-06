@echo off
cd /d "%~dp0"
node "%~dp0jarvis-bot.mjs"
if errorlevel 1 pause
