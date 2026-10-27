param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$project = 'D:\project\ProjectCheon'
$work = Join-Path $project 'Assets\Temp\UiCombatRefinement~'
$stage = Join-Path $work 'Archive_2026-10-27'
$destination = 'E:\C.J.I\Document\10.27작업'
$utf8 = New-Object System.Text.UTF8Encoding($true)
function WriteText([string]$Path, [string]$Content) { [IO.File]::WriteAllText($Path, $Content, $utf8) }
function EnsureDirectory([string]$Path) { [void][IO.Directory]::CreateDirectory($Path) }
function CopyAsset([string]$Source, [string]$Relative, [bool]$Meta = $false) {
    $target = Join-Path $stage $Relative
    EnsureDirectory (Split-Path -Parent $target)
    Copy-Item -LiteralPath $Source -Destination $target
    if ($Meta) {
        if (!(Test-Path -LiteralPath ($Source + '.meta'))) { throw "Missing meta: $Source" }
        Copy-Item -LiteralPath ($Source + '.meta') -Destination ($target + '.meta')
    }
}
if ($Publish) {
    $stageFull = [IO.Path]::GetFullPath($stage)
    $targetFull = [IO.Path]::GetFullPath($destination)
    if ($targetFull -ne 'E:\C.J.I\Document\10.27작업') { throw 'Unexpected export target' }
    $manifest = Get-Content -LiteralPath (Join-Path $stage '파일목록.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $files = @(Get-ChildItem -LiteralPath $stage -Recurse -File)
    foreach ($entry in $manifest.files) {
        $source = Join-Path $stage $entry.path
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Staging changed: $($entry.path)" }
    }
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($stageFull.Length + 1)
        $target = Join-Path $targetFull $relative
        if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
            throw "Existing different file would be overwritten: $target"
        }
    }
    EnsureDirectory $targetFull
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($stageFull.Length + 1)
        $target = Join-Path $targetFull $relative
        EnsureDirectory (Split-Path -Parent $target)
        if (!(Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath $file.FullName -Destination $target }
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw "Copy verification failed: $relative" }
    }
    [pscustomobject]@{ destination=$targetFull; files=$files.Count; bytes=($files | Measure-Object Length -Sum).Sum; verified=$true } | ConvertTo-Json
    exit
}
EnsureDirectory $stage
$folders = '01_적용화면','02_UI텍스처','03_초기시안','04_효과음','05_검증기록','06_적용파일','07_이미지생성기록'
foreach ($folder in $folders) { EnsureDirectory (Join-Path $stage $folder) }
foreach ($name in 'T_Title_Final.png','T_Combat_Final.png','T_HitImpact.png') {
    CopyAsset (Join-Path $work $name) ('01_적용화면\' + $name)
}
$textureNames = 'T_HealingGourd.png','T_InkBrush.png','T_TitleBackground.png','T_TitleLogo.png'
foreach ($name in $textureNames) { CopyAsset (Join-Path $project ('Assets\Arts\UI\MountainPass\' + $name)) ('02_UI텍스처\' + $name) $true }
$generationRoot = 'C:\Users\MBC\.codex\generated_images\01a17c98-c9ce-7193-a831-fce708a4975a'
CopyAsset (Join-Path $generationRoot 'exec-1143d2d1-a56a-40f4-82ef-9f538e7c538c.png') '03_초기시안\T_Combat_Concept.png'
CopyAsset (Join-Path $generationRoot 'exec-21a95e05-c75e-4839-894e-0b804c89656b.png') '03_초기시안\T_Title_Concept.png'
foreach ($audioFile in Get-ChildItem -LiteralPath (Join-Path $project 'Assets\Arts\Audio\MountainPass') -Filter '*.wav') {
    CopyAsset $audioFile.FullName ('04_효과음\' + $audioFile.Name) $true
}
$groups = 'Resources','Healing','HealingActions','DamageAndGround','TransitionsAndAudio','ActualCombat','ResultsTransitions'
foreach ($group in $groups) { CopyAsset (Join-Path $work ('Test_' + $group + '.json')) ('05_검증기록\Test_' + $group + '.json') }
foreach ($name in 'VerificationSummary.json','FinalAudit.json') { CopyAsset (Join-Path $work $name) ('05_검증기록\' + $name) }
CopyAsset (Join-Path $work 'ImageGeneration.json') '07_이미지생성기록\ImageGeneration.json'
$scriptRoles = [ordered]@{
    'PassHud'='HUD 색상·수치·배치, 천지인 타이틀, UI 이미지 연결'
    'PassScreenFade'='신규: 페이드 인·아웃 및 전환 중 시간·입력 제어'
    'PassPlayerEnergy'='신규: 도력 소모·재생 및 적에게서 멀어지는 달리기 판정'
    'PassPlayerMovement'='도력에 따른 달리기·회피 제한 및 소모 연결'
    'PassGourdDrinking'='실제 마시는 진행도에 따른 점진 회복과 취소 처리'
    'PassGameFlow'='화면 전환, 재도전·체크포인트 자원 복구, 음향·피격 연결'
    'PassBossEntrance'='보스 등장·스킵에 페이드 적용, 카메라 복원'
    'PassBossPatternController'='적 목록 조회와 보스 각성·소환 음향 연결'
    'PassBossPatternSettings'='보스 최대 체력 및 공격 설정 기본값'
    'PassFanAttackDamageSettings'='직접 공격과 바람 공격의 최종 피해량 설정'
    'PassFanAttackCombat'='명중 위치 피격 효과 및 타격음 연결'
    'PassFanSweepMesh'='지면 투영 시 적 몸체 제외'
    'PassFanWindParticles'='바람 파티클 지면 투영 시 적 몸체 제외'
    'PassHitEffects'='신규: 직접·바람·플레이어 피격 파티클'
    'PassEffects'='기존 피격 호출을 새 효과로 연결'
    'PassPlayerAreaCombat'='기존 범위 공격 경로의 피격 효과 연결'
    'PassAudio'='효과음 15종 연결, 재생과 중복 타격음 제어'
    'PassPlayerAudio'='신규: 이동·공격·장착·회피 동작과 음향 동기화'
}
foreach ($scriptName in $scriptRoles.Keys) {
    $relative = 'Assets\Scripts\MountainPass\' + $scriptName + '.cs'
    CopyAsset (Join-Path $project $relative) ('06_적용파일\' + $relative) $true
}
foreach ($relative in @(
    'Assets\Arts\Effects\HitImpact\Shaders\S_HitImpact.shader',
    'Assets\Arts\Effects\HitImpact\M_HitImpact.mat',
    'Assets\Scenes\MountainPass\MountainPass.unity',
    'Assets\Scenes\MountainPass\MountainPass_Optimized.unity')) {
    CopyAsset (Join-Path $project $relative) ('06_적용파일\' + $relative) $true
}
$report = Get-Content -LiteralPath (Join-Path $work 'VerificationSummary.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$audit = Get-Content -LiteralPath (Join-Path $work 'FinalAudit.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (!$report.passed -or $report.checks -ne 54 -or $report.groups -ne 7) { throw 'Unexpected verification summary' }
$summary = @'
천지인 | 2026년 10월 27일 작업 정리
프로젝트: D:\project\ProjectCheon
보관 위치: E:\C.J.I\Document\10.27작업

00_작업정리.html을 열면 이미지, 작업 내용, 수치, 효과음 미리듣기를 함께 확인할 수 있습니다.

[이번에 적용한 내용]
1. UI: 좌상단 무기 표시와 생명력·도력 이름 제거. 초록 체력과 파란 도력 막대 및 현재/최대 수치는 표시.
   우상단은 조작키, 그 아래 현재 임무. 좌하단 호리병과 하단 중앙 보스 체력 표시 유지.
2. 타이틀: 천지인 제목·먹 배경·산문 일러스트 적용. 시작, 보스 등장/스킵, 메뉴 및 결과 전환에 페이드.
3. 도력: 최대 100. 회피 25, 달리기 초당 5, 적에게서 멀어지는 달리기 초당 10 소모. 평상시 초당 10 재생.
   달리기 소모 중과 회피 중에는 재생하지 않음. 도력이 부족하면 회피 불가, 고갈되면 달리기 중단.
   일시정지·화면 전환 중 도력 유지. 재도전·체크포인트에서 자원 복구.
4. 호리병: 실제 마시는 구간 동안 총 40을 점진 회복. 피격·공격·회피 등으로 중단되면 남은 회복은 지급하지 않음.
   마시기 시작 시 1회 차감하며, 실제 마시기 전에 취소하면 사용 횟수를 보존.
5. 전투 수치: 보스 최대 체력 1000. 직접 1/2/3타는 10/15/40. 바람 공격은 1타 5, 3타 10.
6. 연출: 효과음·환경음 15종. 공격 이펙트의 지면 투영에서 적 콜라이더를 제외하여 몸체로 번지는 문제 수정.
   직접 타격·바람 타격·플레이어 피격에 접촉 위치 파티클 추가.
7. 텍스처: 신규 게임용 텍스처 4종의 원본 및 기본/Standalone/Android/iPhone/WebGL Max Size 모두 1024 이하.
   종횡비 유지: 호리병 1024×1024, 먹 배경 1024×341, 타이틀 배경 1024×576, 제목 1024×341.

[검증]
Unity 6000.3.14f1에서 컴파일·셰이더 오류 없이 확인. 플레이 모드 7개 그룹, 54개 항목 통과.
도력 소모·재생·고갈·일시정지, 완전/중단 회복, 피해량, 지면 투영, 페이드·카메라 복구,
15개 음원 로딩과 실제 출력, 실제 1타 명중(10+5), 승리·패배·재도전 복구를 검증.
검증 JSON은 이번 구현 때 기록한 결과이며 이번 보관 작업에서 게임 테스트를 다시 실행한 것은 아닙니다.

[보완 후보]
• 보스 체력 1000을 기준으로 실제 전투 시간과 도력 소비 체감에 따른 밸런스 조정.
• 현재 합성 효과음의 음량·거리감 세부 조정 및 필요시 녹음 기반 폴리 사운드 보강.

[폴더 안내]
01_적용화면: 실제 Unity 실행 화면 3장(타이틀, 전투 HUD, 피격).
02_UI텍스처: 최종 게임용 PNG 4종과 기존 GUID를 유지한 .meta.
03_초기시안: 디자인 방향 확인용 초기 전투·타이틀 이미지 2장. 최종 UI는 적용화면 참조.
04_효과음: WAV 15종과 .meta. 원래 프로젝트 경로는 Assets/Arts/Audio/MountainPass.
05_검증기록: 최종 통과 기록 7종, 통합 결과, 텍스처·씬 점검 결과.
06_적용파일: 관련 스크립트 18개, 피격 셰이더·머티리얼, 적용 씬 2개의 현재 사본과 .meta.
07_이미지생성기록: 내장 이미지 생성 도구의 프롬프트, 최종 에셋 경로·해상도.
파일목록.json: 보관 파일의 상대 경로·용량·SHA-256(목록 자체 제외).

[기록 범위]
실행 캡처와 초기 시안은 문서용 이미지로 원래 해상도를 보존했습니다. 게임 텍스처 1024 제한과 구분됩니다.
06_적용파일은 변경 관련 파일의 보관용 사본이며 단독 실행 가능한 전체 Unity 프로젝트가 아닙니다.
씬과 스크립트에는 작업 전부터 존재하던 내용도 포함되므로, 이번 변경 범위는 본 문서의 설명을 기준으로 봅니다.
UI 텍스처 원래 위치: Assets/Arts/UI/MountainPass. 이미지 생성 기록에 개별 경로가 있습니다.
'@
WriteText (Join-Path $stage '00_작업요약.txt') $summary
$groupLabels = @{
 Resources='도력·이동 자원'; Healing='호리병 회복·피격 중단'; HealingActions='공격·회피에 의한 회복 중단';
 DamageAndGround='피해량·지면 투영·HUD'; TransitionsAndAudio='화면 전환·음향'; ActualCombat='실제 애니메이션 명중'; ResultsTransitions='결과 화면·재도전'
}
$testRows = ($report.reports | ForEach-Object {
    '<tr><td>' + $groupLabels[$_.group] + '</td><td>' + $_.checks.Count + '</td><td class="pass">통과</td><td><a href="05_검증기록/Test_' + $_.group + '.json">원본 기록</a></td></tr>'
}) -join "`n"
$codeRows = ($scriptRoles.Keys | ForEach-Object {
    '<tr><td><a href="06_적용파일/Assets/Scripts/MountainPass/' + $_ + '.cs">' + $_ + '.cs</a></td><td>' + $scriptRoles[$_] + '</td></tr>'
}) -join "`n"
$soundNames = @{
 FanWhoosh='부채 휘두르기'; WoodStrike='직접 타격'; WindHit='바람 명중'; PlayerHurt='플레이어 피격'; GuardianSlam='보스 내려찍기';
 ShrineBell='산신각 종'; MountainWind='산바람 환경음'; Footstep='발걸음'; Dodge='회피'; GourdDrink='호리병 마시기';
 MenuConfirm='메뉴 선택'; SpiritAwaken='정령·보스 각성'; Victory='승리'; Defeat='패배'; FanEquip='부채 장착'
}
$audioCards = (Get-ChildItem -LiteralPath (Join-Path $stage '04_효과음') -Filter '*.wav' | Sort-Object Name | ForEach-Object {
    '<div class="sound"><strong>' + $soundNames[$_.BaseName] + '</strong><small>' + $_.Name + '</small><audio controls preload="none" src="04_효과음/' + $_.Name + '"></audio></div>'
}) -join "`n"
$html = @'
<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>천지인 · 10.27 작업 기록</title>
<style>
:root{color-scheme:light;--ink:#173c3c;--muted:#60706a;--gold:#9a753a;--paper:#f5f1e9}*{box-sizing:border-box}body{margin:0;background:var(--paper);color:#263a37;font-family:"Malgun Gothic",sans-serif;line-height:1.7}header{background:#163635;color:#f5eddb;padding:56px max(5vw,24px) 44px}header .eyebrow{color:#e3bb76;font-size:13px;letter-spacing:3px}h1{font-size:46px;margin:8px 0}header p{max-width:900px;margin:12px 0;color:#d7e2d9}.badge{display:inline-block;border:1px solid #71978a;border-radius:24px;padding:5px 14px;margin:8px 8px 0 0;font-size:13px}main{max-width:1200px;padding:24px;margin:auto}nav{display:flex;gap:10px 22px;flex-wrap:wrap;padding:18px 0 28px}a{color:#126b68;text-underline-offset:4px}h2{font-size:25px;margin:38px 0 14px;color:var(--ink)}h3{margin:0 0 12px}p{margin:10px 0}.muted,small{color:var(--muted)}.grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}.card,figure{margin:0;background:white;border:1px solid #deded2;border-radius:12px;overflow:hidden}.card{padding:22px}figure img{width:100%;height:auto;display:block}figcaption{padding:15px 18px}figcaption strong,figcaption small{display:block}.hero{grid-column:1/-1}.assets{display:grid;grid-template-columns:repeat(4,1fr);gap:15px}.assets .preview{height:180px;display:flex;align-items:center;justify-content:center;background:repeating-conic-gradient(#d9dcd2 0% 25%,#ebede4 0% 50%) 50%/22px 22px}.assets img{max-height:180px;object-fit:contain}.assets figcaption{font-size:13px;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse;background:#fff;font-size:14px}th,td{text-align:left;padding:12px 14px;border-bottom:1px solid #e2e4da;vertical-align:top}th{background:#e5eadd;color:var(--ink)}.table-wrap{overflow:auto;border-radius:10px;border:1px solid #deded2}.pass{color:#16713f;font-weight:bold}.sounds{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.sound{background:#fff;padding:15px;border:1px solid #deded2;border-radius:9px}.sound strong,.sound small{display:block}.sound audio{width:100%;margin-top:9px;height:36px}.note{padding:18px 22px;border-left:4px solid #b18a4c;background:#ede7d9;margin:18px 0}.footer{font-size:13px;border-top:1px solid #d3d8cc;padding:26px 0;margin-top:40px;overflow-wrap:anywhere}li{margin:7px 0}code{font-size:13px;overflow-wrap:anywhere}@media(max-width:800px){.grid,.sounds{grid-template-columns:1fr}.assets{grid-template-columns:1fr 1fr}header{padding:28px 22px}h1{font-size:36px}main{padding:18px}th,td{padding:9px}}@media print{header{background:#fff;color:#163635;padding:20px}header p{color:#263a37}nav,.sounds audio{display:none}body{background:#fff}.card,figure,tr{break-inside:avoid}h2{break-after:avoid}a{color:inherit}main{max-width:none}}
</style></head><body>
<header><div class="eyebrow">PROJECT CHEON · WORK LOG · 2026.10.27</div><h1>천지인 — UI · 전투 개선</h1><p>화면 구성과 타이틀을 정리하고, 도력·점진 회복·전투 수치를 적용했습니다. 효과음과 피격 연출을 보강한 작업 기록입니다.</p><span class="badge">프로젝트 적용 완료</span><span class="badge">플레이 검증 54항목 통과</span><span class="badge">게임 텍스처 최대 1024</span></header>
<main><nav aria-label="문서 목차"><a href="#screens">적용 화면</a><a href="#changes">작업 내용</a><a href="#assets">UI 텍스처</a><a href="#audio">효과음</a><a href="#verification">검증</a><a href="#files">변경 파일</a><a href="#followup">보완 후보</a><a href="00_작업요약.txt">텍스트 요약</a></nav>
<section id="screens"><h2>실제 프로젝트 적용 화면</h2><div class="grid">
<figure class="hero"><a href="01_적용화면/T_Title_Final.png"><img src="01_적용화면/T_Title_Final.png" alt="천지인 타이틀 화면"></a><figcaption><strong>천지인 타이틀</strong><small>먹과 산문 일러스트, 한글 서예 제목, 시작·도전 메뉴.</small></figcaption></figure>
<figure><a href="01_적용화면/T_Combat_Final.png"><img src="01_적용화면/T_Combat_Final.png" alt="초록 체력과 파란 도력, 조작키와 임무가 표시된 전투 HUD"></a><figcaption><strong>전투 HUD</strong><small>좌상단 자원 수치 · 우상단 조작키와 임무 · 호리병과 보스 체력 유지.</small></figcaption></figure>
<figure><a href="01_적용화면/T_HitImpact.png"><img src="01_적용화면/T_HitImpact.png" alt="실제 부채 공격 명중 순간의 피격 효과"></a><figcaption><strong>실제 공격 명중 순간</strong><small>접촉 위치에 피격 입자와 섬광. 바람 이펙트의 적 몸체 투영 제외.</small></figcaption></figure>
</div><p class="muted">이미지를 클릭하면 원래 크기로 볼 수 있습니다. 캡처는 검증 중 촬영되어 체력·카메라·배치가 시작 상태와 다를 수 있습니다.</p></section>
<section id="changes"><h2>이번 작업 내용</h2><div class="grid"><article class="card"><h3>UI와 화면 전환</h3><ul><li>좌상단 무기 표시와 체력·도력 이름 제거. 초록 체력과 파란 도력, 현재/최대 수치 표시.</li><li>우상단 조작키 아래에 현재 임무 배치. 호리병과 하단 보스 체력 구성 유지.</li><li>제목을 ‘천지인’으로 변경. 시작·보스 등장과 스킵·메뉴·결과 화면에 페이드 적용.</li></ul></article><article class="card"><h3>회복과 타격 연출</h3><ul><li>실제 마시는 구간 동안 체력 40을 점진 회복. 피격·공격·회피로 중단되면 남은 회복 정지.</li><li>마시기 시작 시 1회 차감. 실제 마시기 전 취소는 사용 횟수 보존.</li><li>공격 효과가 적 몸체로 올라가던 지면 투영 수정. 직접·바람·플레이어 피격 효과 추가.</li></ul></article></div>
<h3 style="margin-top:24px">적용 수치</h3><div class="table-wrap"><table><thead><tr><th>항목</th><th>설정</th><th>동작</th></tr></thead><tbody><tr><td>도력 최대</td><td>100</td><td>재도전·체크포인트에서 복구</td></tr><tr><td>회피</td><td>25 / 회</td><td>잔량 부족 시 회피 불가</td></tr><tr><td>달리기</td><td>5 / 초</td><td>도력 고갈 시 달리기 중단</td></tr><tr><td>적을 등진 달리기</td><td>10 / 초</td><td>전투 중 살아 있는 적에게서 멀어지는 이동 방향 판정</td></tr><tr><td>도력 재생</td><td>10 / 초</td><td>달리기 소모·회피 중에는 재생하지 않음. 일시정지·전환 중 유지</td></tr><tr><td>호리병 회복</td><td>40 / 회</td><td>마신 진행도만큼 회복, 중단 시 미회복분 지급 없음</td></tr><tr><td>보스 체력</td><td>1000</td><td>두 적용 씬에 반영</td></tr><tr><td>직접 공격 1 · 2 · 3타</td><td>10 · 15 · 40</td><td>타격별 최종 피해량</td></tr><tr><td>바람 공격</td><td>5 · 10</td><td>1타 바람 5 / 3타 바람 10</td></tr></tbody></table></div></section>
<section id="assets"><h2>최종 UI 텍스처 4종</h2><p>원본과 Unity 기본·Standalone·Android·iPhone·WebGL 임포트의 최대 크기를 1024로 제한했습니다. 종횡비를 유지했고 .meta도 함께 보관했습니다.</p><div class="assets">
<figure><a class="preview" href="02_UI텍스처/T_HealingGourd.png"><img src="02_UI텍스처/T_HealingGourd.png" alt="호리병 아이콘"></a><figcaption><strong>호리병</strong>T_HealingGourd.png<small>1024 × 1024 · 투명</small></figcaption></figure>
<figure><a class="preview" href="02_UI텍스처/T_InkBrush.png"><img src="02_UI텍스처/T_InkBrush.png" alt="먹 배경"></a><figcaption><strong>먹 배경</strong>T_InkBrush.png<small>1024 × 341 · 투명</small></figcaption></figure>
<figure><a class="preview" href="02_UI텍스처/T_TitleBackground.png"><img src="02_UI텍스처/T_TitleBackground.png" alt="타이틀 산문 배경"></a><figcaption><strong>타이틀 배경</strong>T_TitleBackground.png<small>1024 × 576</small></figcaption></figure>
<figure><a class="preview" href="02_UI텍스처/T_TitleLogo.png"><img src="02_UI텍스처/T_TitleLogo.png" alt="천지인 서예 제목"></a><figcaption><strong>제목</strong>T_TitleLogo.png<small>1024 × 341 · 투명</small></figcaption></figure></div>
<p><a href="07_이미지생성기록/ImageGeneration.json">내장 이미지 생성 도구 · 프롬프트 · 에셋 경로 기록</a></p>
<details><summary>초기 디자인 시안 보기</summary><p class="muted">디자인 방향 확인용 시안입니다. 최종 적용 결과는 위 실제 실행 화면을 기준으로 확인하세요.</p><div class="grid"><figure><a href="03_초기시안/T_Combat_Concept.png"><img loading="lazy" src="03_초기시안/T_Combat_Concept.png" alt="초기 전투 UI 시안"></a><figcaption>전투 UI 초기 시안</figcaption></figure><figure><a href="03_초기시안/T_Title_Concept.png"><img loading="lazy" src="03_초기시안/T_Title_Concept.png" alt="초기 타이틀 시안"></a><figcaption>타이틀 초기 시안</figcaption></figure></div></details></section>
<section id="audio"><h2>효과음 · 환경음 15종</h2><p>이번 작업에서 제작·연결한 합성 WAV 음원입니다. 각 항목에서 미리 들을 수 있습니다.</p><div class="sounds">{{AUDIO_CARDS}}</div></section>
<section id="verification"><h2>검증 결과</h2><p><strong>7개 그룹 · 54항목 통과.</strong> Unity 6000.3.14f1에서 컴파일과 피격 셰이더 오류 없이 확인했습니다.</p><div class="table-wrap"><table><thead><tr><th>검증 그룹</th><th>항목 수</th><th>결과</th><th>근거</th></tr></thead><tbody>{{TEST_ROWS}}</tbody></table></div><p><a href="05_검증기록/VerificationSummary.json">통합 검증 결과</a> · <a href="05_검증기록/FinalAudit.json">텍스처·씬·도력 최종 점검</a></p><p class="muted">실제 1타에서 직접 10 + 바람 5가 한 번씩 적용되어 보스 체력 1000 → 985임을 확인했습니다. 검증 기록은 구현 당시 결과이며 보관 작업에서 재실행한 결과가 아닙니다.</p></section>
<section id="files"><h2>변경 파일과 보관 위치</h2><p>관련 스크립트 18개와 피격 셰이더·머티리얼, 적용 씬 2개를 원래 프로젝트 경로 구조로 보관했습니다.</p><div class="table-wrap"><table><thead><tr><th>스크립트</th><th>이번 작업의 역할</th></tr></thead><tbody>{{CODE_ROWS}}</tbody></table></div>
<ul><li>적용 씬: <a href="06_적용파일/Assets/Scenes/MountainPass/MountainPass_Optimized.unity">MountainPass_Optimized.unity</a>, <a href="06_적용파일/Assets/Scenes/MountainPass/MountainPass.unity">MountainPass.unity</a></li><li>피격 연출: <a href="06_적용파일/Assets/Arts/Effects/HitImpact/Shaders/S_HitImpact.shader">S_HitImpact.shader</a>, <a href="06_적용파일/Assets/Arts/Effects/HitImpact/M_HitImpact.mat">M_HitImpact.mat</a></li><li>UI 원래 경로: <code>Assets/Arts/UI/MountainPass</code></li><li>음원 원래 경로: <code>Assets/Arts/Audio/MountainPass</code></li><li><a href="파일목록.json">전체 파일 목록 · 용량 · SHA-256</a></li></ul>
<div class="note">적용 파일은 현재 프로젝트의 관련 파일 사본이며 단독 실행 가능한 전체 프로젝트가 아닙니다. 기존 내용도 포함하므로 이번 작업 범위는 위 변경 설명을 기준으로 봅니다. 문서용 캡처·초기 시안은 원래 해상도를 보존했으며, 게임용 텍스처는 최대 1024 규칙을 따릅니다.</div></section>
<section id="followup"><h2>보완 후보</h2><div class="grid"><article class="card"><h3>전투 체감 조정</h3><p>보스 체력 1000을 기준으로 전투 시간과 회피·달리기 빈도를 관찰하며 도력 소비와 회복 체감을 조정합니다.</p></article><article class="card"><h3>효과음 마무리</h3><p>공격·피격·환경음의 음량과 거리감을 세부 조정하고, 필요하면 녹음 기반 폴리 사운드를 보강합니다.</p></article></div></section>
<footer class="footer">기준일: 2026년 10월 27일 · 프로젝트: D:\project\ProjectCheon<br>보관 위치: E:\C.J.I\Document\10.27작업<br>이 문서와 함께 있는 폴더를 유지하면 이미지·음원·검증 기록 링크를 오프라인으로 열 수 있습니다.</footer>
</main></body></html>
'@
$html = $html.Replace('{{TEST_ROWS}}',$testRows).Replace('{{CODE_ROWS}}',$codeRows).Replace('{{AUDIO_CARDS}}',$audioCards)
WriteText (Join-Path $stage '00_작업정리.html') $html
Add-Type -AssemblyName System.Drawing
$dimensions = @()
foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File -Filter '*.png') {
    $img = [Drawing.Image]::FromFile($file.FullName)
    try {
        $dimensions += [pscustomobject]@{ path=$file.FullName.Substring($stage.Length+1).Replace('\','/'); width=$img.Width; height=$img.Height }
        if ($file.Directory.Name -eq '02_UI텍스처' -and ($img.Width -gt 1024 -or $img.Height -gt 1024)) { throw 'Texture exceeds 1024' }
    } finally { $img.Dispose() }
}
$entries = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object { $_.Name -ne '파일목록.json' } | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{ path=$_.FullName.Substring($stage.Length+1).Replace('\','/'); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$manifest = [ordered]@{ date='2026-10-27'; project=$project; destination=$destination; files=$entries; images=$dimensions }
WriteText (Join-Path $stage '파일목록.json') ($manifest | ConvertTo-Json -Depth 8)
$links = [regex]::Matches($html, '(?:src|href)="([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Where-Object { ! $_.StartsWith('#') }
foreach ($link in $links) { if (!(Test-Path -LiteralPath (Join-Path $stage $link))) { throw "Broken HTML link: $link" } }
if ($html.Contains('{{')) { throw 'Unexpanded report template' }
[pscustomobject]@{ stage=$stage; files=$entries.Count+1; images=$dimensions.Count; soundClips=$audioCards.Split(@('<audio '),[StringSplitOptions]::None).Length-1; linksVerified=$links.Count; checks=$report.checks } | ConvertTo-Json
