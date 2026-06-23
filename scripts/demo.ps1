#Requires -Version 5.1
<#
.SYNOPSIS
    Start the WFM demo, open the UI, and tear it all down when you stop.
.DESCRIPTION
    Builds and starts the full stack (Postgres + migrations + API + React UI) via
    Docker Compose, waits for the API to be healthy, opens the browser, then waits.
    Press Enter to stop and remove everything (containers + volume).
.PARAMETER NoBuild
    Skip the image rebuild for a faster restart when nothing has changed.
.EXAMPLE
    ./scripts/demo.ps1
.EXAMPLE
    ./scripts/demo.ps1 -NoBuild
#>
[CmdletBinding()]
param(
    [switch]$NoBuild
)

# Note: not 'Stop' -- native tools like docker write normal progress to stderr,
# which 'Stop' would treat as a fatal error. We gate on $LASTEXITCODE instead.
$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot
$uiUrl = 'http://localhost:5173'
$healthUrl = 'http://localhost:8080/health'

Push-Location $repoRoot
try {
    $up = @('compose', 'up', '-d')
    if (-not $NoBuild) { $up += '--build' }

    Write-Host 'Starting the WFM demo stack (first run can take a few minutes to build)...' -ForegroundColor Cyan
    docker @up
    if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed.' }

    Write-Host 'Waiting for the API to become healthy...' -ForegroundColor Cyan
    $healthy = $false
    for ($i = 0; $i -lt 60; $i++) {
        try {
            if ((Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) {
                $healthy = $true
                break
            }
        } catch {
            # Not up yet -- wait and retry.
        }
        Start-Sleep -Seconds 2
    }

    if ($healthy) {
        Write-Host "API is healthy. Opening $uiUrl" -ForegroundColor Green
    } else {
        Write-Warning "API was not healthy in time; opening $uiUrl anyway."
    }

    try { Start-Process $uiUrl } catch { Write-Warning "Couldn't open a browser automatically -- open $uiUrl manually." }

    Write-Host ''
    Write-Host "Demo running at $uiUrl  --  press Enter to stop and tear it down." -ForegroundColor Yellow
    [void](Read-Host)
} finally {
    Write-Host 'Tearing down the demo stack...' -ForegroundColor Cyan
    docker compose down -v
    Pop-Location
}
