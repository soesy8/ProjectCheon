$ErrorActionPreference = 'Stop'
$taskWorkPath = 'D:\project\ProjectCheon\Assets\Temp\CheonAnimationSetup~'
$taskPreparation = Get-Content -LiteralPath (Join-Path $taskWorkPath 'preparation_report.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskValidation = Get-Content -LiteralPath (Join-Path $taskWorkPath 'validation_report.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $taskValidation.passed) { throw '검증을 통과한 수정본만 저장할 수 있습니다.' }
$taskAllowedTargets = @(
    'E:\C.J.I\Blender\AutoRigging\Cheon_Player_Mixamo_RigFixed.blend',
    'E:\C.J.I\Blender\AutoRigging\Cheon_Player_Mixamo_RigFixed.fbx'
)
$taskItems = foreach ($taskBackup in $taskPreparation.backups) {
    $taskTarget = [System.IO.Path]::GetFullPath($taskBackup.source)
    if ($taskTarget -notin $taskAllowedTargets) { throw "허용되지 않은 저장 경로입니다: $taskTarget" }
    $taskCandidate = Join-Path (Join-Path $taskWorkPath 'Candidate') ([System.IO.Path]::GetFileName($taskTarget))
    $taskExpectedCandidateHash = $taskPreparation.candidate_hashes.([System.IO.Path]::GetFileName($taskTarget))
    if ((Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskBackup.sha256) {
        throw "작업 도중 원본이 변경되어 저장을 중단합니다: $taskTarget"
    }
    if ((Get-FileHash -LiteralPath $taskCandidate -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskExpectedCandidateHash) {
        throw "검증 후 수정본이 변경되었습니다: $taskCandidate"
    }
    if ((Get-FileHash -LiteralPath $taskBackup.backup -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskBackup.sha256) {
        throw "원본 백업의 해시가 일치하지 않습니다: $($taskBackup.backup)"
    }
    [pscustomobject]@{ target = $taskTarget; candidate = $taskCandidate; backup = $taskBackup.backup; sha256 = $taskExpectedCandidateHash }
}
$taskSaved = @()
try {
    foreach ($taskItem in $taskItems) {
        Copy-Item -LiteralPath $taskItem.candidate -Destination $taskItem.target -Force
        $taskSaved += $taskItem
        if ((Get-FileHash -LiteralPath $taskItem.target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskItem.sha256) {
            throw "저장 결과의 해시가 일치하지 않습니다: $($taskItem.target)"
        }
    }
} catch {
    foreach ($taskItem in $taskSaved) {
        Copy-Item -LiteralPath $taskItem.backup -Destination $taskItem.target -Force
    }
    throw
}
$taskResult = [pscustomobject]@{
    overwritten = @($taskItems | ForEach-Object { [pscustomobject]@{ path = $_.target; bytes = (Get-Item -LiteralPath $_.target).Length; sha256 = $_.sha256 } })
    validation = $taskValidation
    original_backups = @($taskItems.backup)
    unity_code_and_scenes_modified = $false
}
$taskResult | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $taskWorkPath 'saved_files_report.json') -Encoding UTF8
$taskResult | ConvertTo-Json -Depth 10 -Compress
