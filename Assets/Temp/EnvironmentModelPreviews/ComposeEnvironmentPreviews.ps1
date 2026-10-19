$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourceFolder = 'E:\C.J.I\Blender'
$previewFolder = $PSScriptRoot
$models = @(
    @{ Folder = '01_Lantern'; Asset = 'Cheon_Lantern'; Name = '석등'; English = 'LANTERN' },
    @{ Folder = '02_Grass'; Asset = 'Cheon_Grass'; Name = '풀'; English = 'GRASS' },
    @{ Folder = '03_Flowers'; Asset = 'Cheon_Flowers'; Name = '꽃'; English = 'FLOWERS' },
    @{ Folder = '04_Gate'; Asset = 'Cheon_Gate'; Name = '문'; English = 'GATE' },
    @{ Folder = '05_Wall'; Asset = 'Cheon_Wall'; Name = '담장'; English = 'WALL' },
    @{ Folder = '06_Bell_Pavilion'; Asset = 'Cheon_BellPavilion'; Name = '종각'; English = 'BELL PAVILION' },
    @{ Folder = '07_Mountain_Spirit_Shrine'; Asset = 'Cheon_MountainSpiritShrine'; Name = '산신각'; English = 'MOUNTAIN SPIRIT SHRINE' }
)

function Write-AssetMeta([string]$assetPath, [bool]$isFolder = $false) {
    $metaPath = $assetPath + '.meta'
    if (Test-Path -LiteralPath $metaPath) { return }
    $guid = [guid]::NewGuid().ToString('N')
    $content = "fileFormatVersion: 2`nguid: $guid`n"
    if ($isFolder) {
        $content += "folderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n"
    } elseif ([IO.Path]::GetExtension($assetPath) -eq '.png') {
        $content += "TextureImporter:`n  externalObjects: {}`n  serializedVersion: 13`n  mipmaps:`n    enableMipMap: 0`n    sRGBTexture: 1`n  maxTextureSize: 2048`n  textureType: 0`n  textureShape: 1`n  textureCompression: 0`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n"
    } else {
        $content += "DefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n"
    }
    [IO.File]::WriteAllText($metaPath, $content, [Text.UTF8Encoding]::new($false))
}

function New-PreviewSheet([object[]]$items, [string]$title, [string]$subtitle, [int]$columns, [int]$tileSize, [string]$fileName) {
    $margin = 32
    $gap = 20
    $headerHeight = 138
    $labelHeight = 94
    $rows = [int][Math]::Ceiling($items.Count / [double]$columns)
    $width = $margin * 2 + $columns * $tileSize + ($columns - 1) * $gap
    $height = $headerHeight + $rows * ($tileSize + $labelHeight) + ($rows - 1) * $gap + $margin
    $bitmap = [Drawing.Bitmap]::new($width, $height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([Drawing.Color]::FromArgb(245, 243, 237))
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $darkBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(35, 43, 38))
    $mutedBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(105, 112, 102))
    $cardBrush = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    $titleFont = [Drawing.Font]::new('Malgun Gothic', 31, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
    $nameFont = [Drawing.Font]::new('Malgun Gothic', 27, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
    $smallFont = [Drawing.Font]::new('Malgun Gothic', 17, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
    try {
        $graphics.DrawString($title, $titleFont, $darkBrush, [single]$margin, [single]26)
        $graphics.DrawString($subtitle, $smallFont, $mutedBrush, [single]$margin, [single]82)
        for ($index = 0; $index -lt $items.Count; $index++) {
            $item = $items[$index]
            $x = $margin + ($index % $columns) * ($tileSize + $gap)
            $y = $headerHeight + [int][Math]::Floor($index / [double]$columns) * ($tileSize + $labelHeight + $gap)
            $graphics.FillRectangle($cardBrush, $x, $y, $tileSize, ($tileSize + $labelHeight))
            $imagePath = Join-Path $previewFolder ('T_' + $item.Asset + '_Preview.png')
            $sourceImage = [Drawing.Image]::FromFile($imagePath)
            try {
                # 원본 렌더의 비율과 색상을 유지하여 모델 형태를 그대로 보여 준다.
                $scale = [Math]::Min($tileSize / [double]$sourceImage.Width, $tileSize / [double]$sourceImage.Height)
                $drawWidth = [int]($sourceImage.Width * $scale)
                $drawHeight = [int]($sourceImage.Height * $scale)
                $rectangle = [Drawing.Rectangle]::new(($x + [int](($tileSize - $drawWidth) / 2)), ($y + [int](($tileSize - $drawHeight) / 2)), $drawWidth, $drawHeight)
                $graphics.DrawImage($sourceImage, $rectangle)
            } finally {
                $sourceImage.Dispose()
            }
            $graphics.DrawString($item.Name, $nameFont, $darkBrush, [single]($x + 22), [single]($y + $tileSize + 12))
            $graphics.DrawString($item.English, $smallFont, $mutedBrush, [single]($x + 23), [single]($y + $tileSize + 54))
        }
        $outputPath = Join-Path $previewFolder $fileName
        $bitmap.Save($outputPath, [Drawing.Imaging.ImageFormat]::Png)
        Write-AssetMeta $outputPath
        Write-Output "$outputPath (${width}x${height})"
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
        $titleFont.Dispose()
        $nameFont.Dispose()
        $smallFont.Dispose()
        $darkBrush.Dispose()
        $mutedBrush.Dispose()
        $cardBrush.Dispose()
    }
}

Write-AssetMeta $previewFolder $true
Write-AssetMeta $PSCommandPath
foreach ($model in $models) {
    $fileName = 'T_' + $model.Asset + '_Preview.png'
    $sourcePath = Join-Path (Join-Path $sourceFolder $model.Folder) $fileName
    $destinationPath = Join-Path $previewFolder $fileName
    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
    Write-AssetMeta $destinationPath
}

New-PreviewSheet $models[0..2] '천지인 · 소품과 식생' '석등 / 풀 / 꽃' 3 550 'T_Cheon_PropsAndPlants_Preview.png'
New-PreviewSheet $models[3..6] '천지인 · 건축물' '문 / 담장 / 종각 / 산신각' 2 760 'T_Cheon_Architecture_Preview.png'
