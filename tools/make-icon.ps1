<#
    Generates src/Seer/Assets/seer.ico — the app + tray icon.

    Run from the repo root:  powershell -File tools/make-icon.ps1

    The glyph is an eye ("Seer") in the design system's primary accent
    cyan (#4dd8ff, see .agents/rules/seer_design_system.md), drawn on a
    transparent ground so it reads on both light and dark taskbars.

    Entries are written as 32-bit BMP DIBs rather than embedded PNGs:
    PNG-in-ICO is Vista+ only in principle but is handled inconsistently
    by System.Drawing at small sizes, and the tray icon is loaded through
    System.Drawing. DIB is unambiguous everywhere.

    Note: the 256px entry records its size as 0, per the ICO spec. The
    Windows shell reads that correctly (it's what Explorer shows);
    System.Drawing does not, and will never select that entry. This is
    fine — System.Drawing only serves the 16/32px tray icon here.
#>

Add-Type -AssemblyName System.Drawing

$sizes  = @(16, 24, 32, 48, 64, 128, 256)
$accent = [System.Drawing.Color]::FromArgb(255, 0x4d, 0xd8, 0xff)

function New-Glyph([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Eye outline: a lens/vesica built from two mirrored beziers.
    $pad = $s * 0.10
    $midY = $s / 2.0
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddBezier($pad, $midY, $s * 0.30, $s * 0.16, $s * 0.70, $s * 0.16, $s - $pad, $midY)
    $path.AddBezier($s - $pad, $midY, $s * 0.70, $s * 0.84, $s * 0.30, $s * 0.84, $pad, $midY)

    $path.CloseFigure()   # single closed figure — avoids a spur at the cusps

    $penWidth = [Math]::Max(1.25, $s * 0.085)
    $pen = New-Object System.Drawing.Pen($accent, $penWidth)
    $pen.LineJoin = 'Round'
    $g.DrawPath($pen, $path)

    # Pupil: solid, so the mark still reads as an eye at 16px.
    $r = $s * 0.155
    $brush = New-Object System.Drawing.SolidBrush($accent)
    $g.FillEllipse($brush, ($s / 2.0) - $r, $midY - $r, $r * 2, $r * 2)

    $pen.Dispose(); $brush.Dispose(); $path.Dispose(); $g.Dispose()
    return $bmp
}

# --- Assemble the .ico container ---------------------------------------
$images = @()
foreach ($s in $sizes) {
    $bmp = New-Glyph $s
    $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $data = $bmp.LockBits($rect, 'ReadOnly', [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $stride = $data.Stride
    $pixels = New-Object byte[] ($stride * $s)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    # BITMAPINFOHEADER — height is doubled to cover the (unused) AND mask.
    $bw.Write([int]40); $bw.Write([int]$s); $bw.Write([int]($s * 2))
    $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]0)
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)

    # XOR data, bottom-up.
    for ($y = $s - 1; $y -ge 0; $y--) { $bw.Write($pixels, $y * $stride, $s * 4) }

    # AND mask, all zero: 32-bit alpha does the masking.
    $maskRow = [Math]::Floor(($s + 31) / 32) * 4
    $bw.Write((New-Object byte[] ($maskRow * $s)))

    $bw.Flush()
    $images += ,@($s, $ms.ToArray())
    $bmp.Dispose()
}

$out = New-Object System.IO.MemoryStream
$w   = New-Object System.IO.BinaryWriter($out)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$images.Count)   # ICONDIR

$offset = 6 + (16 * $images.Count)
foreach ($img in $images) {
    $s = $img[0]; $bytes = $img[1]
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))               # 0 means 256
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]$bytes.Length); $w.Write([int]$offset)
    $offset += $bytes.Length
}
foreach ($img in $images) { $w.Write($img[1]) }
$w.Flush()

$dest = Join-Path $PSScriptRoot '..\src\Seer\Assets\seer.ico'
[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $dest)).Path + '\seer.ico', $out.ToArray())
Write-Output "wrote seer.ico ($($out.Length) bytes, $($images.Count) sizes)"
