$ErrorActionPreference = 'Stop'
$projectRoot = 'D:\project\ProjectCheon'
$workPath = Join-Path $projectRoot 'Assets\Temp\MobileRevision~'
Add-Type -AssemblyName System.Drawing
$previousHelper = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets\Temp\MobileControlsArtwork\MobileControlImagePackaging.ps1') -Encoding UTF8 -Raw
$imageCode = [regex]::Match($previousHelper, "(?s)@'\r?\n(.*?)\r?\n'@").Groups[1].Value
if (-not $imageCode) { throw 'Image packaging helper was not found.' }
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition ($imageCode.Replace('MobileControlImagePackaging', 'MobileRevisionImagePackaging'))
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition ($imageCode.Replace('MobileControlImagePackaging', 'MobileAdaptiveIconPackaging').Replace('448f', '320f'))
$manifest = Get-Content -Encoding UTF8 -LiteralPath (Join-Path $workPath 'GenerationPrompts.json') -Raw | ConvertFrom-Json
$template = [IO.File]::ReadAllText((Join-Path $projectRoot 'Assets\Arts\UI\MountainPass\MobileControls\T_MobileAttack.png.meta'))

function Add-FolderMeta([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { New-Item -ItemType Directory -Path $Path | Out-Null }
    if (-not (Test-Path -LiteralPath ($Path + '.meta'))) {
        $guid = [Guid]::NewGuid().ToString('N')
        [IO.File]::WriteAllText(($Path + '.meta'), "fileFormatVersion: 2`nguid: $guid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n")
    }
}

function Add-ImageMeta([string]$Path, [bool]$IsIcon) {
    if (Test-Path -LiteralPath ($Path + '.meta')) { return }
    $meta = [regex]::Replace($template, '(?m)^guid: [a-f0-9]+', ('guid: ' + [Guid]::NewGuid().ToString('N')))
    $meta = [regex]::Replace($meta, 'spriteID: [a-f0-9]+', ('spriteID: ' + [Guid]::NewGuid().ToString('N')))
    if ($IsIcon) { $meta = $meta.Replace('textureType: 8', 'textureType: 0').Replace('spriteMode: 1', 'spriteMode: 0') }
    [IO.File]::WriteAllText(($Path + '.meta'), $meta)
}

Add-FolderMeta $workPath
$iconPath = Join-Path $projectRoot 'Assets\Arts\UI\MountainPass\AppIcon'
Add-FolderMeta $iconPath
foreach ($asset in $manifest.assets) {
    $destination = Join-Path $projectRoot $asset.file
    if ($asset.kind -eq 'replace') {
        $staged = Join-Path $workPath 'T_MobileDodgeReplacement.png'
        [MobileRevisionImagePackaging]::Package($asset.sourcePath, $staged) | Out-Null
        Copy-Item -LiteralPath $staged -Destination $destination -Force
        Add-ImageMeta $staged $false
    } elseif ($asset.kind -eq 'foreground') {
        [MobileAdaptiveIconPackaging]::Package($asset.sourcePath, $destination) | Out-Null
    } elseif ($asset.kind -eq 'opaque') {
        $source = [Drawing.Image]::FromFile($asset.sourcePath)
        $output = New-Object Drawing.Bitmap(512, 512)
        $graphics = [Drawing.Graphics]::FromImage($output)
        try {
            $graphics.Clear([Drawing.Color]::FromArgb(20, 43, 48))
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawImage($source, [Drawing.Rectangle]::new(0, 0, 512, 512))
            $output.Save($destination, [Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $output.Dispose(); $source.Dispose() }
    } else {
        [MobileRevisionImagePackaging]::Package($asset.sourcePath, $destination) | Out-Null
    }
    Add-ImageMeta $destination ($asset.kind -in @('foreground', 'opaque'))
}

# Adaptive 아이콘 배경은 단색 텍스처로 분리해 기기 마스크가 부채를 자르지 않게 한다.
$backgroundPath = Join-Path $iconPath 'T_CheonAppIconBackground.png'
$background = New-Object Drawing.Bitmap(512, 512)
$backgroundGraphics = [Drawing.Graphics]::FromImage($background)
try {
    $backgroundGraphics.Clear([Drawing.Color]::FromArgb(20, 43, 48))
    $background.Save($backgroundPath, [Drawing.Imaging.ImageFormat]::Png)
} finally { $backgroundGraphics.Dispose(); $background.Dispose() }
Add-ImageMeta $backgroundPath $true

$verification = @()
foreach ($folder in @((Join-Path $projectRoot 'Assets\Arts\UI\MountainPass\MobileControls'), $iconPath)) {
    foreach ($file in Get-ChildItem -LiteralPath $folder -Filter '*.png' -File) {
        $image = [Drawing.Bitmap]::FromFile($file.FullName)
        try {
            if ($image.Width -ne 512 -or $image.Height -ne 512) { throw "Image is not 512x512: $($file.Name)" }
            $verification += [PSCustomObject]@{ file = $file.Name; width = $image.Width; height = $image.Height; cornerAlpha = $image.GetPixel(0, 0).A }
        } finally { $image.Dispose() }
        $meta = [IO.File]::ReadAllText($file.FullName + '.meta')
        if ([regex]::Matches($meta, 'maxTextureSize: (?!512\b)\d+').Count -gt 0) { throw "Import size is not 512: $($file.Name)" }
    }
}
[IO.File]::WriteAllText((Join-Path $workPath 'ImageVerification.json'), ($verification | ConvertTo-Json))
Get-ChildItem -LiteralPath $workPath -File | Where-Object { $_.Extension -ne '.meta' } | ForEach-Object {
    if (-not (Test-Path -LiteralPath ($_.FullName + '.meta'))) {
        $guid = [Guid]::NewGuid().ToString('N')
        [IO.File]::WriteAllText(($_.FullName + '.meta'), "fileFormatVersion: 2`nguid: $guid`n")
    }
}
$verification | ConvertTo-Json
