param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\ImageToolkit.App\Assets\ImageToolkit.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Bounds,
        [float]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $arc = [System.Drawing.RectangleF]::new(
        $Bounds.X,
        $Bounds.Y,
        $diameter,
        $diameter)
    $path.AddArc($arc, 180, 90)
    $arc.X = $Bounds.Right - $diameter
    $path.AddArc($arc, 270, 90)
    $arc.Y = $Bounds.Bottom - $diameter
    $path.AddArc($arc, 0, 90)
    $arc.X = $Bounds.X
    $path.AddArc($arc, 90, 90)
    $path.CloseFigure()
    return $path
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new(
        $size,
        $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $scale = $size / 256.0
        $bounds = [System.Drawing.RectangleF]::new(
            8 * $scale,
            8 * $scale,
            240 * $scale,
            240 * $scale)
        $path = New-RoundedRectanglePath $bounds (46 * $scale)
        $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            $bounds,
            [System.Drawing.Color]::FromArgb(255, 20, 29, 38),
            [System.Drawing.Color]::FromArgb(255, 31, 49, 60),
            45)
        $graphics.FillPath($background, $path)

        $framePen = [System.Drawing.Pen]::new(
            [System.Drawing.Color]::FromArgb(255, 62, 205, 190),
            [Math]::Max(1.25, 14 * $scale))
        $framePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $framePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $frameRect = [System.Drawing.RectangleF]::new(
            58 * $scale,
            62 * $scale,
            140 * $scale,
            126 * $scale)
        $framePath = New-RoundedRectanglePath $frameRect (17 * $scale)
        $graphics.DrawPath($framePen, $framePath)

        $mountainPen = [System.Drawing.Pen]::new(
            [System.Drawing.Color]::White,
            [Math]::Max(1.1, 12 * $scale))
        $mountainPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $mountainPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $mountainPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $points = @(
            [System.Drawing.PointF]::new(77 * $scale, 163 * $scale),
            [System.Drawing.PointF]::new(112 * $scale, 123 * $scale),
            [System.Drawing.PointF]::new(139 * $scale, 151 * $scale),
            [System.Drawing.PointF]::new(162 * $scale, 132 * $scale),
            [System.Drawing.PointF]::new(184 * $scale, 163 * $scale)
        )
        $graphics.DrawLines($mountainPen, $points)

        $sparkPen = [System.Drawing.Pen]::new(
            [System.Drawing.Color]::FromArgb(255, 255, 202, 75),
            [Math]::Max(1.1, 12 * $scale))
        $sparkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $sparkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $center = [System.Drawing.PointF]::new(199 * $scale, 65 * $scale)
        $graphics.DrawLine(
            $sparkPen,
            $center.X,
            38 * $scale,
            $center.X,
            92 * $scale)
        $graphics.DrawLine(
            $sparkPen,
            172 * $scale,
            $center.Y,
            226 * $scale,
            $center.Y)

        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $images.Add($stream.ToArray())
        $stream.Dispose()
        $sparkPen.Dispose()
        $mountainPen.Dispose()
        $framePath.Dispose()
        $framePen.Dispose()
        $background.Dispose()
        $path.Dispose()
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$directory = Split-Path -Parent $OutputPath
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
$stream = [System.IO.FileStream]::new(
    $OutputPath,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $bytes = $images[$index]
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $bytes.Length
    }

    foreach ($bytes in $images) {
        $writer.Write($bytes)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Host "Generated $OutputPath"
