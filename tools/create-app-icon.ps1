$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$assets = Join-Path $root 'assets'
$iconPath = Join-Path $assets 'app.ico'

New-Item -ItemType Directory -Force -Path $assets | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class NativeIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}
'@

function New-RoundedRectanglePath {
    param(
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height,
        [float] $Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$bitmap = New-Object System.Drawing.Bitmap 64, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$handle = [IntPtr]::Zero

try {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $background = [System.Drawing.Color]::FromArgb(255, 27, 99, 120)
    $backgroundDark = [System.Drawing.Color]::FromArgb(255, 18, 68, 84)
    $accent = [System.Drawing.Color]::FromArgb(255, 249, 181, 51)
    $white = [System.Drawing.Color]::FromArgb(255, 248, 252, 253)

    $rectPath = New-RoundedRectanglePath 4 4 56 56 12
    $gradientRect = [System.Drawing.RectangleF]::new(4, 4, 56, 56)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush -ArgumentList @(
        $gradientRect,
        $background,
        $backgroundDark,
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $graphics.FillPath($brush, $rectPath)
    $brush.Dispose()
    $rectPath.Dispose()

    $pinPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pinPath.AddEllipse(18, 9, 28, 28)
    $pinPath.AddPolygon(@(
        [System.Drawing.PointF]::new(23, 31),
        [System.Drawing.PointF]::new(32, 54),
        [System.Drawing.PointF]::new(41, 31)
    ))
    $pinBrush = New-Object System.Drawing.SolidBrush -ArgumentList $white
    $graphics.FillPath($pinBrush, $pinPath)
    $pinBrush.Dispose()
    $pinPath.Dispose()

    $innerBrush = New-Object System.Drawing.SolidBrush -ArgumentList $background
    $graphics.FillEllipse($innerBrush, 27, 18, 10, 10)
    $innerBrush.Dispose()

    $signalPen = New-Object System.Drawing.Pen -ArgumentList @($accent, 4)
    $signalPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $signalPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($signalPen, 34, 33, 20, 20, 210, 120)
    $graphics.DrawArc($signalPen, 40, 39, 8, 8, 210, 120)
    $signalPen.Dispose()

    $dotBrush = New-Object System.Drawing.SolidBrush -ArgumentList $accent
    $graphics.FillEllipse($dotBrush, 43, 48, 5, 5)
    $dotBrush.Dispose()

    $handle = $bitmap.GetHicon()
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $stream = [System.IO.File]::Create($iconPath)
    try {
        $icon.Save($stream)
    }
    finally {
        $stream.Dispose()
        $icon.Dispose()
    }
}
finally {
    if ($handle -ne [IntPtr]::Zero) {
        [NativeIcon]::DestroyIcon($handle) | Out-Null
    }
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Host "Created $iconPath"
