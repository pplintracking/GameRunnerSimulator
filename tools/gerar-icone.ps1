# Gera assets/app.ico (16-256 px) e assets/logo.png a partir do desenho abaixo.
# Uso (Windows PowerShell): powershell -ExecutionPolicy Bypass -File tools\gerar-icone.ps1
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot '..\assets'
New-Item -ItemType Directory -Force $dir | Out-Null

function New-Logo([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $s / 256.0

    # quadrado arredondado em gradiente blurple
    $pad = 8 * $k; $w = $s - 2 * $pad; $d = 2 * 56 * $k
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($pad, $pad, $d, $d, 180, 90)
    $p.AddArc($pad + $w - $d, $pad, $d, $d, 270, 90)
    $p.AddArc($pad + $w - $d, $pad + $w - $d, $d, $d, 0, 90)
    $p.AddArc($pad, $pad + $w - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $rect = New-Object System.Drawing.RectangleF $pad, $pad, $w, $w
    $c1 = [System.Drawing.Color]::FromArgb(255, 120, 132, 255)
    $c2 = [System.Drawing.Color]::FromArgb(255, 71, 82, 196)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, 45.0
    $g.FillPath($brush, $p)

    # triângulo de play com cantos arredondados
    $tri = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF (118 * $k), (80 * $k)),
        (New-Object System.Drawing.PointF (198 * $k), (128 * $k)),
        (New-Object System.Drawing.PointF (118 * $k), (176 * $k)))
    $tri.AddPolygon($pts)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), (20 * $k)
    $pen.LineJoin = 'Round'
    $g.FillPath([System.Drawing.Brushes]::White, $tri)
    $g.DrawPath($pen, $tri)

    # traços de velocidade (somem nos tamanhos pequenos)
    if ($s -ge 32) {
        $pen2 = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(210, 255, 255, 255)), (15 * $k)
        $pen2.StartCap = 'Round'; $pen2.EndCap = 'Round'
        $g.DrawLine($pen2, (62 * $k), (106 * $k), (94 * $k), (106 * $k))
        $g.DrawLine($pen2, (48 * $k), (150 * $k), (94 * $k), (150 * $k))
    }
    $g.Dispose()
    return $bmp
}

function Get-Png($bmp) {
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

# quadro DIB 32 bpp (BITMAPINFOHEADER + BGRA de baixo pra cima + máscara AND zerada)
function Get-Dib($bmp) {
    $s = $bmp.Width
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms
    $w.Write([UInt32]40); $w.Write([Int32]$s); $w.Write([Int32]($s * 2))
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]0); $w.Write([UInt32]0); $w.Write([Int32]0); $w.Write([Int32]0)
    $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A)
        }
    }
    $maskRow = [int][Math]::Ceiling($s / 32.0) * 4
    $w.Write((New-Object byte[] ($maskRow * $s)))
    $w.Flush()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = @()
foreach ($s in $sizes) {
    $b = New-Logo $s
    if ($s -ge 128) { $frames += , (Get-Png $b) } else { $frames += , (Get-Dib $b) }
    $b.Dispose()
}

$out = New-Object IO.MemoryStream
$bw = New-Object IO.BinaryWriter $out
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $frames[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($f in $frames) { $bw.Write($f) }
$bw.Flush()
[IO.File]::WriteAllBytes((Join-Path $dir 'app.ico'), $out.ToArray())

$logo = New-Logo 256
$logo.Save((Join-Path $dir 'logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()
Write-Output ("app.ico {0} bytes, logo.png ok" -f (Get-Item (Join-Path $dir 'app.ico')).Length)
