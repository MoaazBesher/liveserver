#requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs Universal Live Server to Program Files
.DESCRIPTION
    Copies files, creates shortcuts, adds context menu and PATH entry
#>

$ErrorActionPreference = "Stop"

# ── Paths ─────────────────────────────────────────────────────────────────────
$SourceDir    = Split-Path -Parent $PSScriptRoot
$InstallDir   = "$env:ProgramFiles\UniversalLiveServer"
$StartMenuDir = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Universal Live Server"
$DesktopFile  = "$env:Public\Desktop\Universal Live Server.lnk"
$ExeSource    = "$SourceDir\src\liveServer.exe"
$IcoSource    = "$SourceDir\src\app.ico"

# ── Ensure source exists ─────────────────────────────────────────────────────
if (-not (Test-Path $ExeSource)) {
    Write-Host "ERROR: liveServer.exe not found at: $ExeSource" -Foreground Red
    Write-Host "Run this script from the installer folder inside the project directory." -Foreground Yellow
    exit 1
}

Write-Host "`n╔══════════════════════════════════════════╗" -Foreground Cyan
Write-Host "║     Universal Live Server Installer      ║" -Foreground Cyan
Write-Host "╚══════════════════════════════════════════╝`n" -Foreground Cyan

# ── 1. Copy files ─────────────────────────────────────────────────────────────
Write-Host "[1/5] Installing files..." -Foreground Yellow
if (-not (Test-Path $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null }

Copy-Item -Path "$SourceDir\src\*" -Destination $InstallDir -Recurse -Force
Write-Host "  -> Files copied to: $InstallDir" -Foreground Green

# ── 2. Start Menu shortcut ────────────────────────────────────────────────────
Write-Host "[2/5] Creating Start Menu shortcut..." -Foreground Yellow
if (-not (Test-Path $StartMenuDir)) { New-Item -ItemType Directory -Path $StartMenuDir -Force | Out-Null }

$WScript = New-Object -ComObject WScript.Shell
$Shortcut = $WScript.CreateShortcut("$StartMenuDir\Universal Live Server.lnk")
$Shortcut.TargetPath = "$InstallDir\liveServer.exe"
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.IconLocation = "$InstallDir\app.ico, 0"
$Shortcut.Description = "Universal Live Server - Auto-detect & run any web project"
$Shortcut.Save()
Write-Host "  -> Start Menu shortcut created" -Foreground Green

# ── 3. Desktop shortcut (optional) ────────────────────────────────────────────
Write-Host "[3/5] Desktop shortcut..." -Foreground Yellow
$desktopChoice = Read-Host "  -> Create desktop shortcut? (Y/n)"
if ($desktopChoice -ne "n" -and $desktopChoice -ne "N") {
    $Shortcut = $WScript.CreateShortcut($DesktopFile)
    $Shortcut.TargetPath = "$InstallDir\liveServer.exe"
    $Shortcut.WorkingDirectory = $InstallDir
    $Shortcut.IconLocation = "$InstallDir\app.ico, 0"
    $Shortcut.Description = "Universal Live Server"
    $Shortcut.Save()
    Write-Host "  -> Desktop shortcut created" -Foreground Green
}

# ── 4. Registry: context menu for folders ────────────────────────────────────
Write-Host "[4/5] Adding folder context menu..." -Foreground Yellow
$contextChoice = Read-Host "  -> Add 'Open with Universal Live Server' to folder right-click? (Y/n)"
if ($contextChoice -ne "n" -and $contextChoice -ne "N") {
    $keyPath = "HKCR:\Directory\shell\UniversalLiveServer"
    $cmdPath = "HKCR:\Directory\shell\UniversalLiveServer\command"

    # Create the menu entry
    New-Item -Path $keyPath -Force | Out-Null
    Set-ItemProperty -Path $keyPath -Name "(Default)" -Value "Open with Universal Live Server"
    Set-ItemProperty -Path $keyPath -Name "Icon" -Value "$InstallDir\liveServer.exe,0"

    # Create the command
    New-Item -Path $cmdPath -Force | Out-Null
    Set-ItemProperty -Path $cmdPath -Name "(Default)" -Value "`"$InstallDir\liveServer.exe`" `"%V`""

    Write-Host "  -> Context menu added" -Foreground Green
}

# ── 5. PATH environment variable ─────────────────────────────────────────────
Write-Host "[5/5] Adding to system PATH..." -Foreground Yellow
$pathChoice = Read-Host "  -> Add 'uls' command alias to PATH? (Y/n)"
if ($pathChoice -ne "n" -and $pathChoice -ne "N") {
    # Create a launcher batch file in the install dir
    $ulsBat = "$InstallDir\uls.bat"
@"
@echo off
start "" "%~dp0liveServer.exe" %*
"@ | Set-Content -Path $ulsBat -Encoding ASCII

    # Add to PATH
    $currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    if ($currentPath -notlike "*$InstallDir*") {
        $newPath = $currentPath + ";$InstallDir"
        [Environment]::SetEnvironmentVariable("Path", $newPath, "Machine")
        Write-Host "  -> Added '$InstallDir' to system PATH" -Foreground Green
        Write-Host "  -> Type 'uls' in any terminal to launch" -Foreground Green
    } else {
        Write-Host "  -> Already in PATH" -Foreground Gray
    }

    # Also create u ls.bat in System32 as shortcut
    $sys32Bat = "$env:windir\System32\uls.bat"
@"
@echo off
start "" "$InstallDir\liveServer.exe" %*
"@ | Set-Content -Path $sys32Bat -Encoding ASCII -Force
    Write-Host "  -> 'uls' command available system-wide" -Foreground Green
}

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host "`n╔══════════════════════════════════════════╗" -Foreground Green
Write-Host "║       Installation Complete!             ║" -Foreground Green
Write-Host "╚══════════════════════════════════════════╝" -Foreground Green
Write-Host "`nLaunch from:" -Foreground White
Write-Host "  Start Menu -> Universal Live Server" -Foreground Cyan
Write-Host "  Desktop shortcut" -Foreground Cyan
Write-Host "  Right-click any folder -> Open with Universal Live Server" -Foreground Cyan
Write-Host "  Terminal: uls" -Foreground Cyan
Write-Host "`nTo uninstall, run: $InstallDir\uninstall.ps1`n" -Foreground Gray
