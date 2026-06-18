@echo off
echo ============================================
echo IT Asset Management Dashboard Installer
echo ============================================
echo.

REM Check if Python is installed
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Python is not installed or not in PATH
    echo Please install Python 3.8 or higher from https://www.python.org/
    pause
    exit /b 1
)

echo Python detected. Installing required packages...
echo.

REM Install required packages
pip install streamlit pandas plotly

if %errorlevel% neq 0 (
    echo ERROR: Failed to install packages
    pause
    exit /b 1
)

echo.
echo Installation completed successfully!
echo.
echo To run the dashboard:
echo 1. Double-click 'run_dashboard.bat'
echo 2. Or open command prompt and run: streamlit run main.py
echo.
echo Make sure to place your asset .txt files in the 'assets' folder
echo.
pause