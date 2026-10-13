@echo off
chcp 65001 >nul
title Tetris Telemetri Sunucusu
cd /d "%~dp0"
start "" "http://localhost:8080"
node server.js
pause
