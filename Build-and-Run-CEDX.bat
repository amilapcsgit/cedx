@echo off
setlocal
cd /d "%~dp0"
title CEDX - Build and Run
where dotnet >nul 2>&1
if errorlevel 1 (
 echo Install the Microsoft .NET 8 SDK for Windows, then run this file again.
 echo https://dotnet.microsoft.com/download/dotnet/8.0
 goto :failed
)
if not exist artifacts mkdir artifacts
echo Building CEDX. First build downloads the required .NET packages.
dotnet publish "src\Cedx.App\Cedx.App.csproj" -c Release -r win-x64 --self-contained true -o "artifacts\cedxadnacedexe" --nologo >"artifacts\build.log" 2>&1
if errorlevel 1 (
 type "artifacts\build.log"
 goto :failed
)
echo Build complete: artifacts\cedxadnacedexe\Cedx.App.exe
if /i "%~1"=="--build-only" exit /b 0
start "CEDX" "artifacts\cedxadnacedexe\Cedx.App.exe"
exit /b 0
:failed
echo Build failed. See artifacts\build.log if present.
if /i not "%~1"=="--build-only" pause
exit /b 1
