@echo off
echo Installing IPX Listener Service from Publish directory...

REM Stop and delete existing service if it exists
echo Checking for existing service...
sc query "IPXSalientListenerv2" >nul 2>&1
if %errorlevel% == 0 (
    echo Stopping existing service...
    sc stop "IPXSalientListenerv2"
    timeout /t 3 /nobreak >nul
    echo Deleting existing service...
    sc delete "IPXSalientListenerv2"
    timeout /t 2 /nobreak >nul
)

REM Create the event log source (requires admin privileges)
echo Creating event log source...
powershell -Command "if (-not [System.Diagnostics.EventLog]::SourceExists('IpxListenerService')) { [System.Diagnostics.EventLog]::CreateEventSource('IpxListenerService', 'Application') }"

REM Install the service pointing to self-contained IpxListenerService.exe
echo Installing service...
sc create "IPXSalientListenerv2" binPath= "%~dp0IpxListenerService.exe" start= auto DisplayName= "IPX Salient Listener Service" obj= "LocalSystem"

if %errorlevel% == 0 (
    echo Service created successfully.
    REM Start the service
    echo Starting service...
    sc start "IPXSalientListenerv2"
    if %errorlevel% == 0 (
        echo Service started successfully.
    ) else (
        echo Failed to start service. Check Event Viewer for details.
    )
) else (
    echo Failed to create service.
)

echo Service installation completed.
echo Check Windows Event Viewer (Application log) for service logs.
pause
