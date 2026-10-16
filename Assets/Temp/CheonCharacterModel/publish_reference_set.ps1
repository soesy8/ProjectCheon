$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$referenceRoot = 'E:\C.J.I\ReferenceImage'
$workRoot = 'E:\C.J.I\WorkTemp\ReferenceFinalize_20261015'
$manifestPath = Join-Path $workRoot 'FinalReferenceSet.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$allowedFolders = @('PlayerCharacter', 'PlayerFan')
if ($manifest.outputs.Count -ne 8) { throw 'Expected eight finalized images.' }

# 승인된 정면 원본과 최초 부채 그림은 재현용 입력으로 먼저 보존했는지 확인한다.
$pairs = @(
    @('E:\C.J.I\ReferenceImage\PlayerCharacter\LowPoly\T_PlayerCharacter_LowPoly2x_SmoothFace_Front.png', (Join-Path $workRoot 'Sources\Character_Approved_Front.png')),
    @('E:\C.J.I\ReferenceImage\PlayerFan\T_PlayerFan_Front.png', (Join-Path $workRoot 'Sources\Fan_Original_Painting.png'))
)
foreach ($pair in $pairs) {
    if ((Get-FileHash -LiteralPath $pair[0]).Hash -ne (Get-FileHash -LiteralPath $pair[1]).Hash) { throw 'Reference input backup mismatch.' }
}
foreach ($entry in $manifest.outputs) {
    if ($entry.folder -notin $allowedFolders) { throw 'Unexpected target folder.' }
    $expected = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $referenceRoot $entry.folder) $entry.file))
    if ($expected -ne [System.IO.Path]::GetFullPath($entry.destination)) { throw 'Destination does not match allowed folder.' }
    $img = [System.Drawing.Image]::FromFile($entry.staged)
    try { if ($img.Width -ne 1024 -or $img.Height -ne 1024) { throw 'Image size mismatch.' } } finally { $img.Dispose() }
}
foreach ($entry in $manifest.outputs) {
    Copy-Item -LiteralPath $entry.staged -Destination $entry.destination -Force
    if ((Get-FileHash -LiteralPath $entry.staged).Hash -ne (Get-FileHash -LiteralPath $entry.destination).Hash) { throw 'Published image checksum mismatch.' }
}

# 삭제 범위는 사용자가 정리를 요청한 두 폴더의 기존 LowPoly PNG로 제한한다.
$oldFiles = @(
    'PlayerCharacter\LowPoly\T_PlayerCharacter_LowPoly_Front.png',
    'PlayerCharacter\LowPoly\T_PlayerCharacter_LowPoly_Side.png',
    'PlayerCharacter\LowPoly\T_PlayerCharacter_LowPoly_Top.png',
    'PlayerCharacter\LowPoly\T_PlayerCharacter_LowPoly_Isometric.png',
    'PlayerCharacter\LowPoly\T_PlayerCharacter_LowPoly2x_Front.png',
    'PlayerCharacter\LowPoly\T_PlayerCharacter_LowPoly2x_SmoothFace_Front.png',
    'PlayerFan\LowPoly\T_PlayerFan_LowPoly_Front.png',
    'PlayerFan\LowPoly\T_PlayerFan_LowPoly_Side.png',
    'PlayerFan\LowPoly\T_PlayerFan_LowPoly_Top.png',
    'PlayerFan\LowPoly\T_PlayerFan_LowPoly_Isometric.png'
)
foreach ($relative in $oldFiles) {
    $target = (Resolve-Path -LiteralPath (Join-Path $referenceRoot $relative)).ProviderPath
    $isAllowed = $false
    foreach ($folder in $allowedFolders) {
        $prefix = [System.IO.Path]::GetFullPath((Join-Path $referenceRoot $folder)) + '\'
        if ($target.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { $isAllowed = $true }
    }
    if (-not $isAllowed) { throw 'Deletion target is outside the authorized reference folders.' }
    if ((Get-Item -LiteralPath $target).Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'Unexpected reference link.' }
    Remove-Item -LiteralPath $target -Force
}
foreach ($folder in $allowedFolders) {
    $oldFolder = Join-Path (Join-Path $referenceRoot $folder) 'LowPoly'
    if ((Get-ChildItem -LiteralPath $oldFolder -Force | Measure-Object).Count -eq 0) { Remove-Item -LiteralPath $oldFolder }
    $files = @(Get-ChildItem -LiteralPath (Join-Path $referenceRoot $folder) -File -Recurse -Force)
    if ($files.Count -ne 4) { throw 'Final folder should contain exactly four files.' }
    foreach ($file in $files) {
        $img = [System.Drawing.Image]::FromFile($file.FullName)
        try { Write-Output ($file.FullName + ' [' + $img.Width + ' x ' + $img.Height + ']') } finally { $img.Dispose() }
    }
}
$manifest.status = 'complete'
$manifest | Add-Member -NotePropertyName verification -NotePropertyValue 'Eight PNG files, four per object, all 1024x1024; published checksums matched staging; ten obsolete variants deleted and eight root images replaced.' -Force
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
