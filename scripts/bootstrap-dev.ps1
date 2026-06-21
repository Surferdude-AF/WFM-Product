#requires -Version 5.1
<#
.SYNOPSIS
    Day-1 developer bootstrap for WFM-Product. Installs everything the build
    needs so a fresh laptop is ready to clone, build, and test.

.DESCRIPTION
    Idempotent: safe to re-run. Uses winget. Each tool is checked before
    install, so re-running only fills gaps and reports versions.

    What this installs and WHY (kept honest - only day-1 needs):
      - Git                  source control
      - .NET SDK 10 (LTS)    backend toolchain (build/test/format) - ADR-005
      - Node.js LTS          React/TS frontend toolchain - ADR-005
      - Docker Desktop       local Postgres + Testcontainers.NET integration
                             tests - ADR-002/004/006
      - dotnet-ef            EF Core migrations (forward-only) - ADR-002
      - pnpm (via corepack)  frontend package manager - ADR-007 (monorepo)

    ASCII-only on purpose: parses identically under Windows PowerShell 5.1 on
    any fresh box, regardless of file encoding.

.NOTES
    Run in a NEW terminal afterwards so PATH changes are picked up.
    Docker Desktop must be started manually (and WSL2 enabled) before the
    integration test suite runs - it is not needed for the solution skeleton.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/bootstrap-dev.ps1
    powershell -File scripts/bootstrap-dev.ps1 -WhatIf      # dry run, install nothing
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    # Pin major versions here. Bump deliberately; this file is the source of truth.
    [string]$DotNetSdkVersion = '10',   # LTS line (Nov 2025)
    [switch]$SkipDocker                  # CI / headless boxes don't want Docker Desktop
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Test-Command([string]$Name) {
    [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

# Run a native command without 'Stop' turning its stderr into a terminating
# error (a Windows PowerShell 5.1 gotcha). Returns combined output lines;
# inspect $LASTEXITCODE afterwards.
function Invoke-Native {
    param([Parameter(Mandatory)][scriptblock]$Block)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Block 2>&1 } finally { $ErrorActionPreference = $prev }
}

function Get-Version([string]$Name) {
    if (-not (Test-Command $Name)) { return $null }
    try { (Invoke-Native { & $Name '--version' } | Select-Object -First 1) } catch { '(present)' }
}

function Install-WingetPackage {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Label,
        [string]$ProbeCommand            # if this resolves, treat as already installed
    )

    if ($ProbeCommand -and (Test-Command $ProbeCommand)) {
        $ver = Get-Version $ProbeCommand
        $suffix = ''
        if ($ver) { $suffix = "  ($ver)" }
        Write-Host "  [skip] $Label already present$suffix" -ForegroundColor DarkGray
        return
    }

    if ($PSCmdlet.ShouldProcess($Label, "winget install $Id")) {
        Write-Host "  [install] $Label ..." -ForegroundColor Cyan
        Invoke-Native {
            winget install --exact --id $Id --accept-source-agreements --accept-package-agreements --silent
        } | Write-Host
        # winget returns nonzero when the package is already current. These are
        # success for our purposes (idempotent re-run), so don't treat as failure.
        $benign = @(
            0,
            -1978335189,  # 0x8A15002B NO_APPLICABLE_UPGRADE - already installed and current
            -1978335135   # 0x8A150061 PACKAGE_ALREADY_INSTALLED
        )
        if ($benign -notcontains $LASTEXITCODE) {
            throw "winget failed for $Label ($Id) with exit code $LASTEXITCODE"
        }
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  [ok] $Label already current" -ForegroundColor DarkGray
        }
    }
}

Write-Host "WFM-Product - day-1 dev bootstrap" -ForegroundColor Green
Write-Host "================================="

if (-not (Test-Command winget)) {
    throw "winget not found. Install 'App Installer' from the Microsoft Store, then re-run."
}

Write-Host "`nCore toolchain:" -ForegroundColor Yellow
Install-WingetPackage -Id 'Git.Git'                               -Label 'Git'                       -ProbeCommand 'git'
Install-WingetPackage -Id "Microsoft.DotNet.SDK.$DotNetSdkVersion" -Label ".NET SDK $DotNetSdkVersion" -ProbeCommand 'dotnet'
Install-WingetPackage -Id 'OpenJS.NodeJS.LTS'                     -Label 'Node.js LTS'               -ProbeCommand 'node'

if (-not $SkipDocker) {
    Write-Host "`nContainers (Postgres + integration tests):" -ForegroundColor Yellow
    Install-WingetPackage -Id 'Docker.DockerDesktop' -Label 'Docker Desktop' -ProbeCommand 'docker'
} else {
    Write-Host "`n[--SkipDocker] skipping Docker Desktop" -ForegroundColor DarkGray
}

# --- Toolchain-managed tools (need the SDKs above on PATH first) -------------
# Re-resolve PATH for this session in case winget just installed dotnet/node.
$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('Path', 'User')

if (Test-Command dotnet) {
    Write-Host "`n.NET global tools:" -ForegroundColor Yellow
    $efInstalled = (Invoke-Native { dotnet tool list --global } | Select-String -SimpleMatch 'dotnet-ef')
    if ($efInstalled) {
        Write-Host "  [skip] dotnet-ef already installed" -ForegroundColor DarkGray
    } elseif ($PSCmdlet.ShouldProcess('dotnet-ef', 'dotnet tool install --global')) {
        Write-Host "  [install] dotnet-ef ..." -ForegroundColor Cyan
        Invoke-Native { dotnet tool install --global dotnet-ef } | Write-Host
    }
} else {
    Write-Host "`n[warn] dotnet not on PATH yet - open a new terminal and re-run to install dotnet-ef" -ForegroundColor Yellow
}

if (Test-Command node) {
    Write-Host "`nFrontend package manager:" -ForegroundColor Yellow
    if ($PSCmdlet.ShouldProcess('pnpm', 'corepack enable')) {
        Invoke-Native { corepack enable } | Out-Null
        Write-Host "  [ok] corepack enabled (pnpm available)" -ForegroundColor DarkGray
    }
}

# --- Verification summary ----------------------------------------------------
Write-Host "`nInstalled versions:" -ForegroundColor Green
foreach ($t in 'git','dotnet','node','docker') {
    $ver = Get-Version $t
    if (-not $ver) { $ver = 'NOT FOUND - open a new terminal, or check the install above' }
    '  {0,-8} {1}' -f $t, $ver | Write-Host
}

Write-Host "`nDone. Open a NEW terminal so PATH updates take effect." -ForegroundColor Green
Write-Host "Then start Docker Desktop before running the integration test suite." -ForegroundColor Green
