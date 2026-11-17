param([Parameter(Mandatory)][string]$OutIco, [string]$PreviewPng)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function New-StopwatchBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 256.0
    $accent = [System.Drawing.Color]::FromArgb(255, 0, 103, 192)
    $accentDark = [System.Drawing.Color]::FromArgb(255, 0, 74, 140)
    $face = [System.Drawing.Color]::FromArgb(255, 250, 251, 253)
    $hand = [System.Drawing.Color]::FromArgb(255, 32, 36, 44)
    $red = [System.Drawing.Color]::FromArgb(255, 214, 48, 70)

    # 小尺寸加粗線條，維持辨識度
    $boost = if ($size -le 24) { 1.6 } elseif ($size -le 48) { 1.25 } else { 1.0 }

    # 上方按鈕與連接柄
    $b = [System.Drawing.SolidBrush]::new($accentDark)
    $g.FillRectangle($b, [float](113*$s), [float](14*$s), [float](30*$s), [float](34*$s))
    $g.FillRectangle($b, [float](92*$s), [float](8*$s), [float](72*$s), [float](20*$s))
    if ($size -ge 32) {
        # 右上側鍵（16/24 省略）
        $m = $g.Transform
        $g.TranslateTransform([float](206*$s), [float](56*$s)); $g.RotateTransform(45)
        $g.FillRectangle($b, [float](-12*$s), [float](-14*$s), [float](24*$s), [float](28*$s))
        $g.Transform = $m
    }

    # 外框與錶面
    $cx = 128*$s; $cy = 146*$s; $r = 104*$s
    $g.FillEllipse([System.Drawing.SolidBrush]::new($accent), [float]($cx-$r), [float]($cy-$r), [float](2*$r), [float](2*$r))
    $ri = $r - 18*$s*$boost
    $g.FillEllipse([System.Drawing.SolidBrush]::new($face), [float]($cx-$ri), [float]($cy-$ri), [float](2*$ri), [float](2*$ri))

    # 刻度（大尺寸才畫）
    if ($size -ge 48) {
        $tick = [System.Drawing.Pen]::new($hand, [float](7*$s))
        $tick.StartCap = $tick.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        for ($i = 0; $i -lt 12; $i++) {
            $a = $i * [Math]::PI / 6
            $len = if ($i % 3 -eq 0) { 20 } else { 10 }
            $r1 = $ri - 10*$s; $r2 = $r1 - $len*$s
            $g.DrawLine($tick, [float]($cx + $r1*[Math]::Sin($a)), [float]($cy - $r1*[Math]::Cos($a)),
                               [float]($cx + $r2*[Math]::Sin($a)), [float]($cy - $r2*[Math]::Cos($a)))
        }
    }

    # 時針與秒針：秒針為紅色，指向約 1 點半
    $pen = [System.Drawing.Pen]::new($hand, [float](12*$s*$boost))
    $pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen, [float]$cx, [float]$cy, [float]$cx, [float]($cy - $ri*0.55))
    $sec = [System.Drawing.Pen]::new($red, [float](9*$s*$boost))
    $sec.StartCap = $sec.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $a = [Math]::PI / 4
    $g.DrawLine($sec, [float]$cx, [float]$cy, [float]($cx + $ri*0.78*[Math]::Sin($a)), [float]($cy - $ri*0.78*[Math]::Cos($a)))
    $hub = 11*$s*$boost
    $g.FillEllipse([System.Drawing.SolidBrush]::new($red), [float]($cx-$hub), [float]($cy-$hub), [float](2*$hub), [float](2*$hub))

    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($sz in $sizes) {
    $bmp = New-StopwatchBitmap $sz
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($PreviewPng -and $sz -eq 256) { $bmp.Save($PreviewPng, [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    [pscustomobject]@{ Size = $sz; Bytes = $ms.ToArray() }
}

# ICO：標頭 + 目錄 + PNG 內容（Vista 以後支援 PNG 壓縮項目）
$out = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($out)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $d = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $w.Write([byte]$d); $w.Write([byte]$d); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$img.Bytes.Length); $w.Write([UInt32]$offset)
    $offset += $img.Bytes.Length
}
foreach ($img in $images) { $w.Write($img.Bytes) }
$w.Flush()
[System.IO.File]::WriteAllBytes($OutIco, $out.ToArray())
"wrote $OutIco ($($out.Length) bytes, sizes: $($sizes -join ','))"
