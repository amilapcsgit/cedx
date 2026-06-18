@echo off
setlocal

echo ============================================
echo Starting IT Asset Management Dashboard
echo ============================================
echo.

:: Define your app's main file and port
set APP_FILE=main.py
set PORT=5000
set FIREWALL_RULE_NAME="Streamlit App Port %PORT%"

REM Check if Python is available
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Python is not installed or not in PATH.
    echo Please ensure Python is installed and accessible, or run install.bat first.
    pause
    exit /b 1
)

:: --- Firewall Rule Check and Creation ---
echo.
echo Checking Windows Defender Firewall rule for port %PORT%...
netsh advfirewall firewall show rule name=%FIREWALL_RULE_NAME% >nul 2>&1
if %errorlevel% neq 0 (
    echo Rule not found. Attempting to create new inbound rule for port %PORT%...
    netsh advfirewall firewall add rule name=%FIREWALL_RULE_NAME% dir=in action=allow protocol=TCP localport=%PORT% description="Allows incoming connections for Streamlit app on port %PORT%." profile=private
    if %errorlevel% equ 0 (
        echo Firewall rule "%FIREWALL_RULE_NAME%" created successfully.
    ) else (
        echo ERROR: Failed to create firewall rule.
        echo This script might need to be run as an Administrator to modify firewall rules.
        echo Please ensure your user has permissions to modify firewall rules or add the rule manually.
    )
) else (
    echo Firewall rule "%FIREWALL_RULE_NAME%" already exists.
)

echo.
echo Starting dashboard on http://localhost:%PORT%/
echo Press Ctrl+C to stop the dashboard
echo.

:: --- Launch Streamlit in a separate process and then open browser ---
:: We use 'start /b' to launch Streamlit in the background, allowing the script to continue.
:: Then, we add a short delay to give Streamlit a moment to start listening before opening the browser.
start "" /b python -m streamlit run %APP_FILE% --server.port %PORT% --server.address 0.0.0.0

echo Giving the server a moment to start...
timeout /t 5 /nobreak >nul

:: --- Open Localhost link in default browser ---
echo Opening http://localhost:%PORT%/ in your default browser...
start "" "http://localhost:%PORT%/"

echo.
echo Your Streamlit app is running.
echo Local Access: http://localhost:%PORT%/
echo LAN Access: http://192.168.100.76:%PORT%/ (after firewall rule is active)
echo.

pause