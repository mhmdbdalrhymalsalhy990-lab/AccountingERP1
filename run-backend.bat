@echo off
title AccountingERP - Backend API
echo ========================================================
echo   Starting AccountingERP Backend (ASP.NET Core)
echo   URL: http://localhost:5000
echo   Health Check: http://localhost:5000/health
echo ========================================================
cd /d "%~dp0Backend\src\AccountingERP.API"
dotnet run --no-restore --urls "http://localhost:5000"
pause
