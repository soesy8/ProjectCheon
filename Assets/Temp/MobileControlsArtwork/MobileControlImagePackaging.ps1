param([string]$ProjectRoot = 'D:\project\ProjectCheon')

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# 생성된 원본은 보존하고, 최종 PNG만 공통 여백과 512 해상도로 정규화한다.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class MobileControlImagePackaging
{
    public static string Package(string inputPath, string outputPath)
    {
        if (System.IO.File.Exists(outputPath))
            throw new InvalidOperationException("Refusing to overwrite: " + outputPath);
        using (var source = new Bitmap(inputPath))
        {
            if (source.GetPixel(0, 0).A != 0 || source.GetPixel(source.Width - 1, source.Height - 1).A != 0)
                throw new InvalidOperationException("Transparent exterior is missing: " + inputPath);
            int left = source.Width, top = source.Height, right = -1, bottom = -1;
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                {
                    if (source.GetPixel(x, y).A <= 8) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
            if (right < left) throw new InvalidOperationException("Empty generated image: " + inputPath);
            left = Math.Max(0, left - 3); top = Math.Max(0, top - 3);
            right = Math.Min(source.Width - 1, right + 3); bottom = Math.Min(source.Height - 1, bottom + 3);
            int cropWidth = right - left + 1, cropHeight = bottom - top + 1;
            float scale = 448f / Math.Max(cropWidth, cropHeight);
            int width = (int)Math.Round(cropWidth * scale), height = (int)Math.Round(cropHeight * scale);
            using (var finalImage = new Bitmap(512, 512, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(finalImage))
                using (var attributes = new ImageAttributes())
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(source, new Rectangle((512 - width) / 2, (512 - height) / 2, width, height),
                        left, top, cropWidth, cropHeight, GraphicsUnit.Pixel, attributes);
                }
                finalImage.Save(outputPath, ImageFormat.Png);
            }
            return source.Width + "x" + source.Height + " -> 512x512";
        }
    }

    public static string Validate(string path)
    {
        using (var image = new Bitmap(path))
        {
            if (image.Width != 512 || image.Height != 512) throw new InvalidOperationException("Wrong size: " + path);
            int clear = 0, semi = 0, opaque = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    byte alpha = image.GetPixel(x, y).A;
                    if (alpha == 0) clear++; else if (alpha == 255) opaque++; else semi++;
                    if ((x == 0 || y == 0 || x == 511 || y == 511) && alpha != 0)
                        throw new InvalidOperationException("Nontransparent image border: " + path);
                }
            if (clear == 0 || opaque == 0) throw new InvalidOperationException("Invalid image alpha: " + path);
            return "512x512 RGBA; clear=" + clear + "; antialiased=" + semi + "; opaque=" + opaque;
        }
    }
}
'@

function Add-FolderMeta([string]$FolderPath) {
    if (-not (Test-Path -LiteralPath $FolderPath)) { New-Item -ItemType Directory -Path $FolderPath | Out-Null }
    $metaPath = $FolderPath + '.meta'
    if (-not (Test-Path -LiteralPath $metaPath)) {
        $guid = [Guid]::NewGuid().ToString('N')
        [IO.File]::WriteAllText($metaPath, "fileFormatVersion: 2`nguid: $guid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n")
    }
}

function Add-DefaultMeta([string]$AssetPath) {
    $metaPath = $AssetPath + '.meta'
    if (-not (Test-Path -LiteralPath $metaPath)) {
        $guid = [Guid]::NewGuid().ToString('N')
        [IO.File]::WriteAllText($metaPath, "fileFormatVersion: 2`nguid: $guid`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n")
    }
}

$workPath = Join-Path $ProjectRoot 'Assets\Temp\MobileControlsArtwork'
$artPath = Join-Path $ProjectRoot 'Assets\Arts\UI\MountainPass\MobileControls'
Add-FolderMeta $workPath
Add-FolderMeta $artPath
Get-ChildItem -LiteralPath $workPath -File | Where-Object { $_.Extension -ne '.meta' } | ForEach-Object { Add-DefaultMeta $_.FullName }
$manifest = Get-Content -LiteralPath (Join-Path $workPath 'GenerationPrompts.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$template = [IO.File]::ReadAllText((Join-Path $ProjectRoot 'Assets\Arts\UI\MountainPass\T_HealingGourd.png.meta'))
$template = [regex]::Replace($template, 'maxTextureSize: \d+', 'maxTextureSize: 512')
$template = $template.Replace('spriteGenerateFallbackPhysicsShape: 1', 'spriteGenerateFallbackPhysicsShape: 0')

$verification = @()
foreach ($asset in $manifest.assets) {
    $destination = Join-Path $artPath $asset.fileName
    $normalization = [MobileControlImagePackaging]::Package($asset.sourcePath, $destination)
    $metaPath = $destination + '.meta'
    if (Test-Path -LiteralPath $metaPath) { throw "Refusing to overwrite existing metadata: $metaPath" }
    $meta = [regex]::Replace($template, '(?m)^guid: [a-f0-9]+', ('guid: ' + [Guid]::NewGuid().ToString('N')))
    $meta = [regex]::Replace($meta, 'spriteID: [a-f0-9]+', ('spriteID: ' + [Guid]::NewGuid().ToString('N')))
    [IO.File]::WriteAllText($metaPath, $meta)
    $alphaResult = [MobileControlImagePackaging]::Validate($destination)
    $verification += [PSCustomObject]@{ file = $asset.fileName; normalization = $normalization; image = $alphaResult; sprite = $true; maxSize = 512 }
}

# 비교용 시트는 완성 에셋 폴더와 분리하여 Temp에만 저장한다.
$previewPath = Join-Path $workPath 'T_MobileControlsPreview.png'
$sheet = New-Object System.Drawing.Bitmap(1024, 712)
$graphics = [Drawing.Graphics]::FromImage($sheet)
$graphics.Clear([Drawing.Color]::FromArgb(18, 34, 39))
$graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$headingFont = New-Object Drawing.Font('Malgun Gothic', 23, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel))
$labelFont = New-Object Drawing.Font('Malgun Gothic', 18, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel))
$smallFont = New-Object Drawing.Font('Malgun Gothic', 13, ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel))
$paperBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(242, 230, 198))
$jadeBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(157, 206, 191))
$graphics.DrawString('모바일 조작 버튼 이미지', $headingFont, $paperBrush, 32, 22)
$graphics.DrawString('512 × 512 PNG  ·  투명 배경  ·  이미지 에셋 전용', $smallFont, $jadeBrush, 33, 61)
$labels = @('공격', '회피', '호리병 회복', '보스 주시', '메뉴 / 일시정지', '상호작용', '이동 스틱 바탕', '이동 스틱 손잡이')
$textFormat = New-Object Drawing.StringFormat
$textFormat.Alignment = [Drawing.StringAlignment]::Center
$textFormat.LineAlignment = [Drawing.StringAlignment]::Center
for ($index = 0; $index -lt $manifest.assets.Count; $index++) {
    $column = $index % 4
    $row = [Math]::Floor($index / 4)
    $tileX = 16 + $column * 248
    $tileY = 100 + $row * 298
    $image = [Drawing.Image]::FromFile((Join-Path $artPath $manifest.assets[$index].fileName))
    try { $graphics.DrawImage($image, [Drawing.Rectangle]::new($tileX, $tileY, 240, 240)) } finally { $image.Dispose() }
    $labelRect = [Drawing.RectangleF]::new($tileX, $tileY + 235, 240, 34)
    $nameRect = [Drawing.RectangleF]::new($tileX - 4, $tileY + 267, 248, 23)
    $graphics.DrawString($labels[$index], $labelFont, $paperBrush, $labelRect, $textFormat)
    $graphics.DrawString($manifest.assets[$index].fileName, $smallFont, $jadeBrush, $nameRect, $textFormat)
}
$sheet.Save($previewPath, [Drawing.Imaging.ImageFormat]::Png)
$textFormat.Dispose(); $paperBrush.Dispose(); $jadeBrush.Dispose()
$headingFont.Dispose(); $labelFont.Dispose(); $smallFont.Dispose()
$graphics.Dispose(); $sheet.Dispose()
$previewMeta = [regex]::Replace($template, '(?m)^guid: [a-f0-9]+', ('guid: ' + [Guid]::NewGuid().ToString('N')))
$previewMeta = [regex]::Replace($previewMeta, 'spriteID: [a-f0-9]+', ('spriteID: ' + [Guid]::NewGuid().ToString('N')))
$previewMeta = $previewMeta.Replace('maxTextureSize: 512', 'maxTextureSize: 1024')
if (-not (Test-Path -LiteralPath ($previewPath + '.meta'))) { [IO.File]::WriteAllText(($previewPath + '.meta'), $previewMeta) }
$reportPath = Join-Path $workPath 'AssetVerification.json'
[IO.File]::WriteAllText($reportPath, ($verification | ConvertTo-Json -Depth 5))
Add-DefaultMeta $reportPath
$verification | Format-Table -AutoSize
Write-Output $previewPath
