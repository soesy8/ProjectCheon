$ErrorActionPreference = 'Stop'
$projectRoot = (Get-Location).Path
$workDirectory = Join-Path $projectRoot 'Assets\Temp\TextureSourceResize~'
$before = Get-Content -LiteralPath (Join-Path $workDirectory 'Before.json') -Raw | ConvertFrom-Json
$plan = Get-Content -LiteralPath (Join-Path $workDirectory 'ResizePlan.json') -Raw | ConvertFrom-Json
$expected = @{}
foreach ($row in $plan) { $expected[$row.path] = $row }
$afterRows = @()
$violations = @()
foreach ($row in $before) {
    $path = Join-Path $projectRoot $row.path
    $stream = [IO.File]::OpenRead($path)
    try {
        $header = New-Object byte[] 33
        $null = $stream.Read($header, 0, 33)
        $widthBytes = [byte[]]$header[16..19]; [array]::Reverse($widthBytes)
        $heightBytes = [byte[]]$header[20..23]; [array]::Reverse($heightBytes)
        $width = [BitConverter]::ToUInt32($widthBytes, 0)
        $height = [BitConverter]::ToUInt32($heightBytes, 0)
    } finally { $stream.Dispose() }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $metaHash = (Get-FileHash -LiteralPath ($path + '.meta') -Algorithm SHA256).Hash
    if ($width -gt 1024 -or $height -gt 1024) { $violations += "최대 크기: $($row.path)" }
    if ($metaHash -ne $row.metaHash) { $violations += "메타 변경: $($row.path)" }
    if ([int]$header[24] -ne $row.bitDepth -or [int]$header[25] -ne $row.colorType) {
        $violations += "채널 변경: $($row.path)"
    }
    if ($expected.ContainsKey($row.path)) {
        $resized = $expected[$row.path]
        if ($hash -ne $resized.stagedHash -or $width -ne $resized.width -or $height -ne $resized.height) {
            $violations += "축소 결과 불일치: $($row.path)"
        }
        $scale = 1024.0 / [math]::Max($row.width, $row.height)
        if ([math]::Abs($width - $row.width * $scale) -gt 0.500001 -or
            [math]::Abs($height - $row.height * $scale) -gt 0.500001) {
            $violations += "비율 오차: $($row.path)"
        }
    } elseif ($hash -ne $row.hash) { $violations += "대상 외 파일 변경: $($row.path)" }
    $afterRows += [pscustomobject]@{path=$row.path; width=$width; height=$height; bytes=(Get-Item -LiteralPath $path).Length}
}
$assetBefore = Get-Content -LiteralPath (Join-Path $workDirectory 'AssetSize.before.json') -Raw | ConvertFrom-Json
$assetFiles = @(Get-ChildItem -LiteralPath 'Assets' -Recurse -File | Where-Object {
    $_.Extension -ne '.meta' -and $_.FullName -notlike '*\Assets\Temp\*'
})
$report = [pscustomobject]@{
    checkedTextures=$before.Count; resizedTextures=$plan.Count; unchangedTextures=$before.Count-$plan.Count
    beforeTextureBytes=($before | Measure-Object bytes -Sum).Sum
    afterTextureBytes=($afterRows | Measure-Object bytes -Sum).Sum
    savedBytes=($before | Measure-Object bytes -Sum).Sum-($afterRows | Measure-Object bytes -Sum).Sum
    beforeAssetContentBytes=$assetBefore.bytes
    afterAssetContentBytes=($assetFiles | Measure-Object Length -Sum).Sum
    stagedImageFiles=@(Get-ChildItem -LiteralPath (Join-Path $workDirectory 'Staged') -File -Recurse).Count
    violations=$violations
    nonSquareTextures=@($afterRows | Where-Object { $expected.ContainsKey($_.path) -and $_.width -ne $_.height })
}
[IO.File]::WriteAllText((Join-Path $workDirectory 'Validation.json'), ($report | ConvertTo-Json -Depth 7), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 7
if ($violations.Count -gt 0) { throw '원본 텍스처 검증에 실패했습니다.' }
