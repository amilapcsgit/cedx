@echo off
setlocal
cd /d "%~dp0"
title CEDX - Astra mission control
where dotnet >nul 2>&1
if errorlevel 1 (
 echo Install the .NET 8 SDK for Windows x64, then run astraui.bat again.
 goto :failed
)
if not exist artifacts mkdir artifacts
echo Building Astra UI. Close Astra before rebuilding.
dotnet publish "src\Cedx.App\Cedx.App.csproj" -c Release -r win-x64 --self-contained true -o "artifacts\astra" --nologo >"artifacts\astra-build.log" 2>&1
if errorlevel 1 (
 type "artifacts\astra-build.log"
 goto :failed
)
if /i "%~1"=="--build-only" exit /b 0
start "CEDX Astra" "artifacts\astra\Cedx.App.exe" --astra
exit /b 0
:failed
echo Build failed. See artifacts\astra-build.log if present.
if /i not "%~1"=="--build-only" pause
exit /b 1
