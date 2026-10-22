# Deploy script for IPX Listener Service to IIS
param(
    [string]$TargetDir = "C:\IPX_Listener_Service_Ver2.0.1",
    [string]$AppPoolName = ""
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " IPX Listener Service - IIS Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Target Directory: $TargetDir"

$ScriptDir = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "publish"
if (-not (Test-Path $ScriptDir)) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

# 1. Stop IIS or AppPool to release DLL file locks
Write-Host "`n[1/4] Stopping IIS / releasing file locks..." -ForegroundColor Yellow
if ($AppPoolName -ne "") {
    try {
        & %windir%\system32\inetsrv\appcmd.exe stop apppool /apppool.name:"$AppPoolName"
    } catch {
        Write-Warning "Could not stop AppPool via appcmd, stopping IIS..."
        iisreset /stop
    }
} else {
    iisreset /stop
}

# 2. Ensure target directory exists
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}

# 3. Copy published files
Write-Host "`n[2/4] Copying files from $ScriptDir to $TargetDir..." -ForegroundColor Yellow
Get-ChildItem -Path $ScriptDir -Exclude "deploy-to-iis.ps1" | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $TargetDir -Recurse -Force
}

# 4. Restart IIS
Write-Host "`n[3/4] Restarting IIS..." -ForegroundColor Yellow
if ($AppPoolName -ne "") {
    try {
        & %windir%\system32\inetsrv\appcmd.exe start apppool /apppool.name:"$AppPoolName"
    } catch {
        iisreset /start
    }
} else {
    iisreset /start
}

Write-Host "`n[4/4] Deployment completed successfully!" -ForegroundColor Green
Write-Host "Check Swagger at: http://localhost/swagger" -ForegroundColor Cyan
Write-Host "Check Health at:  http://localhost/api/health" -ForegroundColor Cyan
