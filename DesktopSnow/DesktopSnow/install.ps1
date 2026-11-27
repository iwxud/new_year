param(
    [switch]$AutoStart,
    [switch]$DoNotLaunch
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceExe = Join-Path $projectRoot 'dist\DesktopSnow.exe'
$sourceConfig = Join-Path $projectRoot 'dist\DesktopSnow.exe.config'

if (-not (Test-Path $sourceExe)) {
    & (Join-Path $projectRoot 'build.ps1')
}

$installDirectory = Join-Path $env:LOCALAPPDATA 'DesktopSnow'
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null

$targetExe = Join-Path $installDirectory 'DesktopSnow.exe'
Copy-Item $sourceExe $targetExe -Force
Copy-Item $sourceConfig (Join-Path $installDirectory 'DesktopSnow.exe.config') -Force

$shell = New-Object -ComObject WScript.Shell
$desktopShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Desktop Snow.lnk'))
$desktopShortcut.TargetPath = $targetExe
$desktopShortcut.WorkingDirectory = $installDirectory
$desktopShortcut.IconLocation = "$targetExe,0"
$desktopShortcut.Description = 'White snow on the desktop'
$desktopShortcut.Save()

$programsDirectory = [Environment]::GetFolderPath('Programs')
$startMenuShortcut = $shell.CreateShortcut((Join-Path $programsDirectory 'Desktop Snow.lnk'))
$startMenuShortcut.TargetPath = $targetExe
$startMenuShortcut.WorkingDirectory = $installDirectory
$startMenuShortcut.IconLocation = "$targetExe,0"
$startMenuShortcut.Description = 'White snow on the desktop'
$startMenuShortcut.Save()

if ($AutoStart) {
    $startupParameters = @{
        Path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
        Name = 'DesktopSnow'
        PropertyType = 'String'
        Value = ('"' + $targetExe + '" --startup')
        Force = $true
    }
    New-ItemProperty @startupParameters | Out-Null
}

Write-Host "Desktop Snow installed: $targetExe" -ForegroundColor Green

if (-not $DoNotLaunch) {
    Start-Process $targetExe
}
