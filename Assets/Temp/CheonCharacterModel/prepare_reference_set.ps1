$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$workRoot = 'E:\C.J.I\WorkTemp\ReferenceFinalize_20261015'
$manifestPath = Join-Path $workRoot 'FinalReferenceSet.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($entry in $manifest.outputs) {
    $source = [System.Drawing.Image]::FromFile($entry.source)
    $bitmap = [System.Drawing.Bitmap]::new(1024, 1024, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $canvas = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $canvas.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $canvas.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $canvas.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $canvas.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, 1024, 1024))
        $bitmap.Save($entry.staged, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $canvas.Dispose()
        $bitmap.Dispose()
        $source.Dispose()
    }
    $check = [System.Drawing.Image]::FromFile($entry.staged)
    try {
        if ($check.Width -ne 1024 -or $check.Height -ne 1024) { throw 'Unexpected image dimensions' }
    } finally { $check.Dispose() }
    Write-Output $entry.staged
}
