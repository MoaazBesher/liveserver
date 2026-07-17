#requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls Universal Live Server
.DESCRIPTION
    Removes files, shortcuts, context menu, and PATH entries
#>

$ErrorActionPreference = "Stop"

$InstallDir   = "$env:ProgramFiles\UniversalLiveServer"
$StartMenuDir = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Universal Live Server"
$DesktopFile  = "$env:Public\Desktop\Universal Live Server.lnk"

Write-Host "`n╔══════════════════════════════════════════╗" -Foreground Red
Write-Host "║    Universal Live Server Uninstaller     ║" -Foreground Red
Write-Host "╚══════════════════════════════════════════╝`n" -Foreground Red

$confirm = Read-Host "Are you sure you want to uninstall Universal Live Server? (y/N)"
if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "Uninstall cancelled." -Foreground Gray
    exit
}

Write-Host "[1/5] Removing Start Menu shortcut..." -Foreground Yellow
if (Test-Path $StartMenuDir) {
    Remove-Item -Path "$StartMenuDir\*" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $StartMenuDir -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Start Menu shortcut removed" -Foreground Green
}

Write-Host "[2/5] Removing Desktop shortcut..." -Foreground Yellow
if (Test-Path $DesktopFile) {
    Remove-Item -Path $DesktopFile -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Desktop shortcut removed" -Foreground Green
}

Write-Host "[3/5] Removing folder context menu..." -Foreground Yellow
$keyPath = "HKCR:\Directory\shell\UniversalLiveServer"
if (Test-Path $keyPath) {
    Remove-Item -Path $keyPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Context menu removed" -Foreground Green
}

Write-Host "[4/5] Removing PATH entries..." -Foreground Yellow
$sys32Bat = "$env:windir\System32\uls.bat"
if (Test-Path $sys32Bat) {
    Remove-Item -Path $sys32Bat -Force -ErrorAction SilentlyContinue
    Write-Host "  -> 'uls' command removed" -Foreground Green
}

$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($currentPath -like "*$InstallDir*") {
    $newPath = ($currentPath -split ';' | Where-Object { $_ -ne $InstallDir }) -join ';'
    [Environment]::SetEnvironmentVariable("Path", $newPath, "Machine")
    Write-Host "  -> Removed from system PATH" -Foreground Green
}

Write-Host "[5/5] Removing program files..." -Foreground Yellow
if (Test-Path $InstallDir) {
    try {
        Remove-Item -Path "$InstallDir\*" -Recurse -Force -ErrorAction Stop
        Remove-Item -Path $InstallDir -Force -ErrorAction Stop
        Write-Host "  -> Program files removed" -Foreground Green
    } catch {
        Write-Host "  -> Some files may be in use. Close the app and try again." -Foreground Yellow
    }
}

Write-Host "`n╔══════════════════════════════════════════╗" -Foreground Green
Write-Host "║       Uninstallation Complete!            ║" -Foreground Green
Write-Host "╚══════════════════════════════════════════╝`n" -Foreground Green
