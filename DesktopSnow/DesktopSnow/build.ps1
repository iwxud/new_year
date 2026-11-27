$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDirectory = Join-Path $projectRoot 'src'
$assetsDirectory = Join-Path $projectRoot 'assets'
$outputDirectory = Join-Path $projectRoot 'dist'
$iconPath = Join-Path $assetsDirectory 'DesktopSnow.ico'
$outputPath = Join-Path $outputDirectory 'DesktopSnow.exe'
$manifestPath = Join-Path $projectRoot 'app.manifest'

New-Item -ItemType Directory -Path $assetsDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

& (Join-Path $projectRoot 'tools\GenerateIcon.ps1') -OutputPath $iconPath

$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $frameworkRoot 'csc.exe'))) {
    $frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}

$compiler = Join-Path $frameworkRoot 'csc.exe'
if (-not (Test-Path $compiler)) {
    throw '.NET Framework 4.x compiler (csc.exe) was not found.'
}

$references = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'WindowsBase.dll',
    'PresentationCore.dll',
    'PresentationFramework.dll',
    'System.Xaml.dll'
)

$arguments = @(
    '/nologo',
    '/codepage:65001',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    ('/win32manifest:' + $manifestPath),
    ('/win32icon:' + $iconPath),
    ('/out:' + $outputPath)
)

foreach ($reference in $references) {
    $referencePath = Join-Path $frameworkRoot $reference
    if (-not (Test-Path $referencePath)) {
        $referencePath = Join-Path (Join-Path $frameworkRoot 'WPF') $reference
    }
    if (-not (Test-Path $referencePath)) {
        throw "Framework reference was not found: $reference"
    }
    $arguments += ('/reference:' + $referencePath)
}

$sourceFiles = Get-ChildItem -Path $sourceDirectory -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName }
$arguments += $sourceFiles

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Copy-Item (Join-Path $projectRoot 'DesktopSnow.exe.config') (Join-Path $outputDirectory 'DesktopSnow.exe.config') -Force

$file = Get-Item $outputPath
Write-Host ('Built: {0} ({1:N0} bytes)' -f $file.FullName, $file.Length) -ForegroundColor Green
