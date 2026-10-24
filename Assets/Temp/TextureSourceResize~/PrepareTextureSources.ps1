$ErrorActionPreference = 'Stop'
$projectRoot = (Get-Location).Path
$workDirectory = Join-Path $projectRoot 'Assets\Temp\TextureSourceResize~'
$stagedRoot = Join-Path $workDirectory 'Staged'
[IO.Directory]::CreateDirectory($stagedRoot) | Out-Null
Add-Type -AssemblyName System.Drawing
$referenceAssemblies = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object { $_.FullName })
$referenceAssemblies += [System.Drawing.Bitmap].Assembly.Location
$referenceAssemblies += @(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll' | ForEach-Object { $_.FullName })
Add-Type -Path (Join-Path $workDirectory 'TextureSourceResizer.cs') -ReferencedAssemblies $referenceAssemblies

function Get-PngInformation([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    try {
        $header = New-Object byte[] 33
        if ($stream.Read($header, 0, 33) -ne 33 -or $header[0] -ne 137 -or $header[1] -ne 80) {
            throw "PNG 파일이 아닙니다: $path"
        }
        $widthBytes = [byte[]]$header[16..19]
        $heightBytes = [byte[]]$header[20..23]
        [array]::Reverse($widthBytes)
        [array]::Reverse($heightBytes)
        return [pscustomobject]@{
            width = [BitConverter]::ToUInt32($widthBytes, 0)
            height = [BitConverter]::ToUInt32($heightBytes, 0)
            bitDepth = [int]$header[24]
            colorType = [int]$header[25]
        }
    } finally { $stream.Dispose() }
}

$rows = @(rg -l --glob '*.meta' '^TextureImporter:' Assets | ForEach-Object {
    $metaPath = $_
    $relativePath = $metaPath.Substring(0, $metaPath.Length - 5).Replace('\', '/')
    $absolutePath = Join-Path $projectRoot $relativePath
    $information = Get-PngInformation $absolutePath
    $meta = [IO.File]::ReadAllText((Join-Path $projectRoot $metaPath))
    [pscustomobject]@{
        path = $relativePath
        width = $information.width
        height = $information.height
        bitDepth = $information.bitDepth
        colorType = $information.colorType
        bytes = (Get-Item -LiteralPath $absolutePath).Length
        hash = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash
        metaHash = (Get-FileHash -LiteralPath $metaPath -Algorithm SHA256).Hash
        normalMap = [regex]::Match($meta, '(?m)^  textureType: (\d+)').Groups[1].Value -eq '1'
    }
})
[IO.File]::WriteAllText((Join-Path $workDirectory 'Before.json'), ($rows | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
$assetFiles = @(Get-ChildItem -LiteralPath 'Assets' -Recurse -File | Where-Object {
    $_.Extension -ne '.meta' -and $_.FullName -notlike '*\Assets\Temp\*'
})
[IO.File]::WriteAllText((Join-Path $workDirectory 'AssetSize.before.json'),
    ([pscustomobject]@{bytes=($assetFiles | Measure-Object Length -Sum).Sum; count=$assetFiles.Count} | ConvertTo-Json),
    [Text.UTF8Encoding]::new($false))

$targets = @($rows | Where-Object { $_.width -gt 1024 -or $_.height -gt 1024 })
$plan = @()
foreach ($target in $targets) {
    $sourcePath = Join-Path $projectRoot $target.path
    $stagedPath = Join-Path $stagedRoot $target.path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($stagedPath)) | Out-Null
    $independentAlpha = $target.path -match '(Metallic|Smoothness|Roughness|_AO|Mask|Normal)'
    [TextureSourceResizer]::Resize($sourcePath, $stagedPath, 1024, $target.colorType -eq 6, $target.normalMap, $independentAlpha)
    $resized = Get-PngInformation $stagedPath
    $scale = 1024.0 / [math]::Max($target.width, $target.height)
    $expectedWidth = [math]::Round($target.width * $scale, [MidpointRounding]::AwayFromZero)
    $expectedHeight = [math]::Round($target.height * $scale, [MidpointRounding]::AwayFromZero)
    if ($resized.width -ne $expectedWidth -or $resized.height -ne $expectedHeight -or
        $resized.width -gt 1024 -or $resized.height -gt 1024 -or
        $resized.bitDepth -ne $target.bitDepth -or $resized.colorType -ne $target.colorType) {
        throw "축소 파일의 크기 또는 채널 형식이 올바르지 않습니다: $($target.path)"
    }
    $stagedBytes = (Get-Item -LiteralPath $stagedPath).Length
    if ($stagedBytes -ge $target.bytes) { throw "축소 후 용량이 줄지 않았습니다: $($target.path)" }
    # PNG 전체를 다시 디코딩해 이미지 데이터와 색상 메타데이터가 정상인지 확인한다.
    $decoded = [System.Drawing.Bitmap]::new($stagedPath, $false)
    try { $null = $decoded.GetPixel(0, 0); $null = $decoded.GetPixel($decoded.Width - 1, $decoded.Height - 1) }
    finally { $decoded.Dispose() }
    $plan += [pscustomobject]@{
        path=$target.path; stagedPath=$stagedPath; beforeWidth=$target.width; beforeHeight=$target.height
        width=$resized.width; height=$resized.height; beforeBytes=$target.bytes; bytes=$stagedBytes
        beforeHash=$target.hash; stagedHash=(Get-FileHash -LiteralPath $stagedPath -Algorithm SHA256).Hash
        metaHash=$target.metaHash; colorType=$target.colorType; normalMap=$target.normalMap
    }
    if ($plan.Count % 5 -eq 0) { Write-Output "검증된 축소 파일: $($plan.Count)/$($targets.Count)" }
}
[IO.File]::WriteAllText((Join-Path $workDirectory 'ResizePlan.json'), ($plan | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
[pscustomobject]@{
    textureCount=$rows.Count; resizeCount=$plan.Count
    beforeTextureBytes=($rows | Measure-Object bytes -Sum).Sum
    beforeChangedBytes=($plan | Measure-Object beforeBytes -Sum).Sum
    afterChangedBytes=($plan | Measure-Object bytes -Sum).Sum
    savedBytes=($plan | Measure-Object beforeBytes -Sum).Sum - ($plan | Measure-Object bytes -Sum).Sum
} | ConvertTo-Json
