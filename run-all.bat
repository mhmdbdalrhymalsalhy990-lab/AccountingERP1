@echo off
title AccountingERP Launcher
echo ========================================================
echo   Launching Accounting ERP System
echo ========================================================
echo.
echo [1/2] Starting Backend API on http://localhost:5000 ...
start "AccountingERP - Backend API" "%~dp0run-backend.bat"
timeout /t 3 /nobreak >nul
echo.
echo [2/2] Starting Frontend UI on http://localhost:4200 ...
start "AccountingERP - Frontend UI" "%~dp0run-frontend.bat"
echo.
echo ========================================================
echo   Both services are launching in separate windows!
echo   - Backend API:  http://localhost:5000
echo   - Frontend App: http://localhost:4200
echo   - Credentials:  admin / Admin@123456
echo ========================================================
timeout /t 5
