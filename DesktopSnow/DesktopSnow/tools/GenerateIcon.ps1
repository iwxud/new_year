param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$directory = Split-Path -Parent $OutputPath
if ($directory -and -not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$bitmap = New-Object System.Drawing.Bitmap 64, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$background = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Rectangle 3, 3, 58, 58),
    [System.Drawing.Color]::FromArgb(255, 35, 134, 220),
    [System.Drawing.Color]::FromArgb(255, 9, 67, 148),
    90.0)
$graphics.FillEllipse($background, 3, 3, 58, 58)

$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 4.2
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

for ($arm = 0; $arm -lt 3; $arm++) {
    $angle = $arm * [Math]::PI / 3.0
    $dx = [Math]::Cos($angle) * 20.0
    $dy = [Math]::Sin($angle) * 20.0
    $graphics.DrawLine($pen, [single](32 - $dx), [single](32 - $dy), [single](32 + $dx), [single](32 + $dy))
}

$centerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$graphics.FillEllipse($centerBrush, 28.5, 28.5, 7, 7)

$graphics.Dispose()
$background.Dispose()
$pen.Dispose()
$centerBrush.Dispose()

$handle = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $stream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create)
    try {
        $icon.Save($stream)
    }
    finally {
        $stream.Dispose()
        $icon.Dispose()
    }
}
finally {
    Add-Type -Namespace NativeMethods -Name IconMethods -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool DestroyIcon(System.IntPtr handle);
'@ -ErrorAction SilentlyContinue
    [NativeMethods.IconMethods]::DestroyIcon($handle) | Out-Null
    $bitmap.Dispose()
}
