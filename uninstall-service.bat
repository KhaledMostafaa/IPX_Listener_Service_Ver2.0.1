@echo off
echo Uninstalling IPX Listener Service...

REM Stop the service
echo Stopping service...
sc stop "IPXSalientListenerv2"

REM Wait a moment for service to stop
timeout /t 5 /nobreak

REM Delete the service
echo Removing service...
sc delete "IPXSalientListenerv2"

echo Service uninstallation completed.
pause