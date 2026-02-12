# Claude Unity Bridge - Quick Installer (PowerShell)
#
# Usage:
#   irm https://raw.githubusercontent.com/ManageXR/claude-unity-bridge/main/install.ps1 | iex
#
# Or download and run:
#   .\install.ps1

$ErrorActionPreference = "Stop"

Write-Host "Installing Claude Unity Bridge..." -ForegroundColor Cyan
Write-Host ""

# Check Python
$pythonCmd = $null
foreach ($cmd in @("python", "python3", "py")) {
    try {
        $version = & $cmd --version 2>&1
        if ($version -match "Python 3\.") {
            $pythonCmd = $cmd
            break
        }
    }
    catch {
        continue
    }
}

if (-not $pythonCmd) {
    Write-Host "Error: Python 3 is required but not installed." -ForegroundColor Red
    Write-Host "Download from: https://www.python.org/downloads/" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found Python: $pythonCmd" -ForegroundColor Green

# Install via pip (user-site to avoid permission issues)
Write-Host ""
Write-Host "Installing pip package..." -ForegroundColor Cyan
& $pythonCmd -m pip install --user --upgrade claude-unity-bridge
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: pip installation failed" -ForegroundColor Red
    exit 1
}

# Get Python user scripts directory
$userBase = & $pythonCmd -m site --user-base
$scriptsDir = Join-Path $userBase "Scripts"

# Add to PATH if not already there
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$scriptsDir*") {
    Write-Host "Adding Python scripts to user PATH..." -ForegroundColor Cyan

    $newPath = if ($userPath) { "$userPath;$scriptsDir" } else { $scriptsDir }
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")

    # Update current session PATH
    $env:Path = "$env:Path;$scriptsDir"

    Write-Host "PATH updated. You may need to restart your terminal for changes to take effect." -ForegroundColor Yellow
}
else {
    Write-Host "Python scripts directory already in PATH" -ForegroundColor Green
}

# Install skill
Write-Host ""
Write-Host "Installing Claude Code skill..." -ForegroundColor Cyan
& $pythonCmd -m claude_unity_bridge.cli install-skill
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Skill installation failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Add the Unity package to your project:"
Write-Host "     Window > Package Manager > + > Add package from git URL..."
Write-Host "     https://github.com/ManageXR/claude-unity-bridge.git?path=package"
Write-Host ""
Write-Host "  2. Open Claude Code in your Unity project directory"
Write-Host ""
Write-Host "  3. Ask Claude naturally: " -NoNewline
Write-Host '"Run the Unity tests"' -ForegroundColor Yellow
Write-Host ""
