# Generates assets\app.ico (16-256 px, PNG-compressed frames): a dark keyboard with four glowing zones.
# Usage: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "assets\app.ico"
$sizes = 16, 24, 32, 48, 64, 128, 256
$zones = @(
    [Drawing.Color]::FromArgb(0, 110, 255),
    [Drawing.Color]::FromArgb(0, 210, 255),
    [Drawing.Color]::FromArgb(150, 0, 255),
    [Drawing.Color]::FromArgb(255, 0, 150)
)

function RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

function Frame([int]$s) {
    $bmp = New-Object Drawing.Bitmap $s, $s
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([Drawing.Color]::Transparent)
    $k = $s / 32.0
    # keyboard body
    $body = RoundedPath (1 * $k) (6 * $k) (30 * $k) (20 * $k) (4 * $k)
    $grad = New-Object Drawing.Drawing2D.LinearGradientBrush ((New-Object Drawing.RectangleF 0, (6 * $k), $s, (20 * $k))), ([Drawing.Color]::FromArgb(44, 48, 60)), ([Drawing.Color]::FromArgb(20, 22, 28)), 90
    $g.FillPath($grad, $body)
    $pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(110, 118, 140)), ([Math]::Max(1, 0.9 * $k))
    $g.DrawPath($pen, $body)
    for ($z = 0; $z -lt 4; $z++) {
        $c = $zones[$z]
        $x = (4.5 + $z * 6) * $k
        if ($s -ge 48) {
            # glow + three key rows per zone at larger sizes
            $glow = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(70, $c))
            $g.FillPath($glow, (RoundedPath ($x - 0.8 * $k) (9.2 * $k) (6.6 * $k) (13.6 * $k) (1.8 * $k)))
            $b = New-Object Drawing.SolidBrush $c
            for ($r = 0; $r -lt 3; $r++) { $g.FillPath($b, (RoundedPath $x ((10 + $r * 4.2) * $k) (5 * $k) (3.2 * $k) (0.9 * $k))) }
        } else {
            $b = New-Object Drawing.SolidBrush $c
            $g.FillPath($b, (RoundedPath $x (10 * $k) (5 * $k) (12 * $k) ([Math]::Max(0.5, 1.5 * $k))))
        }
    }
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    if ($s -ge 256) {
        # 256 px: PNG-compressed frame (standard for large icons)
        $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    } else {
        # smaller sizes: classic 32-bit DIB frame (BITMAPINFOHEADER + bottom-up BGRA + empty AND mask),
        # which every reader including System.Drawing.Icon understands
        $bw = New-Object IO.BinaryWriter $ms
        $bw.Write([uint32]40); $bw.Write([int32]$s); $bw.Write([int32]($s * 2)); $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]0); $bw.Write([uint32]($s * $s * 4)); $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
        for ($y = $s - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $s; $x++) { $c = $bmp.GetPixel($x, $y); $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A) } }
        $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
        $bw.Write((New-Object byte[] ($maskRow * $s)))
        $bw.Flush()
    }
    $bmp.Dispose()
    return , $ms.ToArray()
}

$frames = foreach ($s in $sizes) { , (Frame $s) }
$fs = [IO.File]::Create($out); $w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)   # ICONDIR
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {                                      # ICONDIRENTRY
    $s = $sizes[$i]; $len = $frames[$i].Length
    $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256)); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$len); $w.Write([uint32]$offset)
    $offset += $len
}
foreach ($f in $frames) { $w.Write($f) }
$w.Close()
"Wrote $out ($((Get-Item $out).Length) bytes, sizes: $($sizes -join ', '))"
