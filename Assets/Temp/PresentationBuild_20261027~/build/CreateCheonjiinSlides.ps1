param([string]$Revision = 'v1')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRoot = 'D:/project/ProjectCheon'
$workRoot = "$taskRoot/Assets/Temp/PresentationBuild_20261027~"
$outputRoot = "$taskRoot/Deliverables/Cheonjiin_20261027"
$background = 'C:/Users/MBC/Downloads/cheonjiin-overview-background-v2.png'
$designSource = 'E:/C.J.I/Document/천지인 기획서 개정본.md'
$script:bodyFont = 'Noto Sans KR'
$script:titleFont = 'Noto Serif KR'
function Color([string]$hex) {
    $hex = $hex.TrimStart('#')
    return [Convert]::ToInt32($hex.Substring(0,2),16) + 256*[Convert]::ToInt32($hex.Substring(2,2),16) + 65536*[Convert]::ToInt32($hex.Substring(4,2),16)
}
$script:ink = Color '263D37'
$script:jade = Color '3F6A5B'
$script:gold = Color 'A58348'
$script:muted = Color '66675C'
$script:textLog = [System.Collections.Generic.List[object]]::new()
function Text($slide,[string]$text,[double]$x,[double]$y,[double]$w,[double]$h,[double]$size=19,[int]$color=$script:ink,[bool]$bold=$false,[string]$font=$script:bodyFont,[int]$align=1) {
    $shape = $slide.Shapes.AddTextbox(1,$x,$y,$w,$h)
    $shape.Name = 'Text_' + $slide.SlideIndex + '_' + $slide.Shapes.Count
    $shape.Fill.Visible = 0; $shape.Line.Visible = 0
    $shape.TextFrame.MarginLeft=0; $shape.TextFrame.MarginRight=0
    $shape.TextFrame.MarginTop=0; $shape.TextFrame.MarginBottom=0
    $shape.TextFrame.WordWrap=-1; $shape.TextFrame.AutoSize=0
    $range=$shape.TextFrame.TextRange
    $range.Text=$text
    $range.Font.Name=$font; $range.Font.NameFarEast=$font
    $range.Font.Size=$size; $range.Font.Color.RGB=$color
    $range.Font.Bold= $(if($bold){-1}else{0})
    $range.ParagraphFormat.Alignment=$align
    $range.ParagraphFormat.SpaceAfter=7
    $range.ParagraphFormat.LineRuleWithin=-1
    $range.ParagraphFormat.SpaceWithin=1.12
    $shape.Left=$x; $shape.Top=$y; $shape.Width=$w; $shape.Height=$h
    $script:textLog.Add([pscustomobject]@{slide=$slide.SlideIndex;name=$shape.Name;text=$text})
}
function NewSlide([string]$title,[string]$notes) {
    $slide=$script:deck.Slides.Add($script:deck.Slides.Count+1,12)
    $slide.FollowMasterBackground=0
    $slide.Background.Fill.UserPicture($background)
    if($title){Text $slide $title 68 48 824 57 31 $script:ink $false $script:titleFont}
    Text $slide ('{0:00}' -f $slide.SlideIndex) 867 503 28 20 10.5 $script:gold $false $script:bodyFont 3
    $slide.NotesPage.Shapes.Placeholders.Item(2).TextFrame.TextRange.Text=$notes+"`n`n출처: $designSource (v0.2, 2026-10-26). 프로젝트 확인 기준: 2026-10-27, 저장된 파일과 직렬화 참조. 새 플레이 실행 및 배포 빌드는 검증하지 않았다. 화면 이미지는 프로젝트에 보관된 기존 캡처다. 테마: 사용자가 제공한 cheonjiin-overview-background-v2.png."
    return $slide
}
function Photo($slide,[string]$relative,[double]$x,[double]$y,[double]$w,[double]$h,[string]$caption='프로젝트 보관 캡처',[double[]]$crop=@()) {
    $full=Join-Path $taskRoot $relative
    if(!(Test-Path -LiteralPath $full)){throw "Missing image $full"}
    $img=[System.Drawing.Image]::FromFile($full)
    $iw=$img.Width; $ih=$img.Height; $img.Dispose()
    $pic=$slide.Shapes.AddPicture($full,0,-1,$x,$y,$iw,$ih)
    $pic.Name='Picture_'+$slide.SlideIndex+'_'+$slide.Shapes.Count
    $pic.AlternativeText=$caption+'; '+$relative
    if($crop.Count -eq 4){
        $pic.PictureFormat.CropLeft=$crop[0]
        $pic.PictureFormat.CropTop=$crop[1]
        $pic.PictureFormat.CropRight=$iw-$crop[0]-$crop[2]
        $pic.PictureFormat.CropBottom=$ih-$crop[1]-$crop[3]
        $iw=$crop[2];$ih=$crop[3]
    }
    $ratio=[Math]::Min($w/$iw,$h/$ih)
    $pic.LockAspectRatio=0
    $pic.Width=$iw*$ratio; $pic.Height=$ih*$ratio
    $pic.Left=$x+($w-$pic.Width)/2; $pic.Top=$y+($h-$pic.Height)/2
    if($caption){Text $slide $caption $x ($y+$h+8) $w 34 10.5 $script:muted}
}
function Table($slide,$rows,[double]$x,[double]$y,[double]$w,[double]$h,$widths,[double]$size=17.5) {
    $rowCount=$rows.Count; $columnCount=$rows[0].Count
    $shape=$slide.Shapes.AddTable($rowCount,$columnCount,$x,$y,$w,$h)
    $shape.Name='Table_'+$slide.SlideIndex
    $table=$shape.Table
    for($c=1;$c -le $columnCount;$c++){ $table.Columns.Item($c).Width=$widths[$c-1] }
    for($r=1;$r -le $rowCount;$r++){
        $table.Rows.Item($r).Height=$h/$rowCount
        for($c=1;$c -le $columnCount;$c++){
            $cell=$table.Cell($r,$c)
            $cell.Shape.Fill.Solid()
            $cell.Shape.Fill.ForeColor.RGB=$(if($r -eq 1){$script:jade}elseif($r%2 -eq 0){Color 'F9F4E9'}else{Color 'F1EBDD'})
            $cell.Shape.TextFrame.MarginLeft=11; $cell.Shape.TextFrame.MarginRight=8
            $cell.Shape.TextFrame.MarginTop=7; $cell.Shape.TextFrame.MarginBottom=6
            $cell.Shape.TextFrame.VerticalAnchor=3
            $tr=$cell.Shape.TextFrame.TextRange
            $tr.Text=[string]$rows[$r-1][$c-1]
            $tr.Font.Name=$script:bodyFont; $tr.Font.NameFarEast=$script:bodyFont
            $tr.Font.Size=$size
            $tr.Font.Bold=$(if($r -eq 1){-1}else{0})
            $tr.Font.Color.RGB=$(if($r -eq 1){Color 'FFF9EF'}else{$script:ink})
            $tr.ParagraphFormat.SpaceAfter=0
            for($b=1;$b -le 4;$b++){
                $cell.Borders.Item($b).ForeColor.RGB=Color 'DDD4C1'
                $cell.Borders.Item($b).Weight=0.5
            }
        }
    }
}
function HeadingBody($slide,[string]$heading,[string]$body,[double]$x,[double]$y,[double]$w,[double]$bodyH=65) {
    Text $slide $heading $x $y $w 32 21 $script:jade $true
    Text $slide $body $x ($y+39) $w $bodyH 18.5 $script:ink
}
function SmallNote($slide,[string]$text,[double]$y=461){Text $slide $text 68 $y 824 31 11.5 $script:muted}

$script:app=$null; $script:deck=$null
try {
    $script:app=New-Object -ComObject PowerPoint.Application
    $script:app.DisplayAlerts=1
    $script:deck=$script:app.Presentations.Add(0)
    $script:deck.PageSetup.SlideWidth=960; $script:deck.PageSetup.SlideHeight=540

    $s=NewSlide '' '표지. 팀 부채, 팀장·개발 김민성. 프로젝트 가제는 천지인(天地人), 현재 시연명은 천 / CHEON, 시연 부제는 산문을 지키는 자다. 프로젝트 전체 기획과 현재 한 구간 시연을 함께 설명한다. 기획서 0~2장.'
    Text $s '천지인' 150 139 660 103 64 $script:ink $false $script:titleFont 2
    Text $s '天 地 人' 250 241 460 43 24 $script:gold $false $script:titleFont 2
    Text $s '산문을 지키는 자' 160 307 640 44 29 $script:jade $false $script:titleFont 2
    Text $s '한국 설화 판타지 액션 RPG' 180 365 600 30 18 $script:ink $false $script:bodyFont 2
    Text $s '팀 부채   김민성' 250 432 460 26 14 $script:muted $false $script:bodyFont 2
    Text $s '기획서 v0.2 기반 · 2026.10.27 프로젝트 현황 반영' 230 466 500 24 10.5 $script:muted $false $script:bodyFont 2

    $s=NewSlide '프로젝트 개요' '기획서 1~2장. 3D 실시간 전술형 액션 RPG. 한국 설화와 민간신앙, 견습 도사와 부채, 짧은 탐색 및 보스전 시연이 중심이다. PC 키보드·마우스는 현재 조작 기준이며 최종 출시 플랫폼은 미정이다. 개발 환경은 ProjectSettings/ProjectVersion.txt 및 Packages/manifest.json을 확인했다.'
    Text $s "부채를 든 견습 도사가`n영적 산문 너머의 장승에게 도전한다" 68 139 824 100 29 $script:jade $false $script:titleFont
    Table $s @(@('항목','기획 방향'),@('장르','3D 실시간 전술형 액션 RPG'),@('세계·주인공','한국 설화 판타지 · 부채를 사용하는 견습 도사'),@('현재 시연','산길 탐색과 장승 수문장 보스전'),@('시점·입력','3인칭 자유 시점 · PC 키보드와 마우스')) 68 263 824 177 @(170,654) 17
    SmallNote $s 'Unity 6000.3.14f1 · URP 17.3.0     최종 출시 플랫폼과 시연 분량은 추후 확정'

    $s=NewSlide '한국적인 사물과 플레이의 연결' '기획서 2장 설계 원칙, 3장 세계관, 5~7장 플레이어·전투. 전통 소재가 장식으로만 머무르지 않도록 각 사물에 플레이 역할을 부여한다. 산신각의 거점 기능과 장승의 관문 역할을 설명한다.'
    Text $s '견습 도사와 부채' 68 146 270 42 24 $script:jade $true
    Text $s "부채 연격과 바람으로 싸우며`n공격을 이어갈지 회피할지 판단한다" 366 145 518 75 21
    Text $s '산신각의 가호' 68 251 270 42 24 $script:jade $true
    Text $s "기도로 회복하고 다시 도전하는`n수련의 거점" 366 250 518 75 21
    Text $s '장승 수문장' 68 356 270 42 24 $script:jade $true
    Text $s "산문을 지키는 존재의 전조를 읽고`n회피 뒤 반격 기회를 잡는 첫 관문" 366 355 518 75 21

    $s=NewSlide '현실과 영적 세계' '기획서 3장. 현실과 영적 세계는 같은 지형 위에 겹쳐 존재한다. 현재는 산문 통과 후 같은 씬의 배경·조명·안개·하늘 색을 전환한다. 별도 영계 지형이나 양쪽 세계를 동시에 보이는 포털 미리보기의 완성으로 해석하지 않는다. 근거: PassWorldTransition.cs. 이미지: Assets/Temp/SummitMistReveal~/Summit.png 및 Assets/Temp/BossPatterns~/Test_BossPatterns_Play.png. 보관 화면은 과거 시점이다.'
    Text $s '현실의 수련지' 68 126 394 34 21 $script:jade $true
    Text $s '산문 너머의 영적 분위기' 498 126 394 34 21 $script:jade $true
    Photo $s 'Assets/Temp/SummitMistReveal~/Summit.png' 68 173 394 242 '산신각과 소나무 · 프로젝트 보관 캡처'
    Photo $s 'Assets/Temp/BossPatterns~/Test_BossPatterns_Play.png' 498 173 394 242 '보스전 공간 · 과거 테스트 캡처'
    SmallNote $s '현재는 같은 지형의 환경 전환을 구현했다. 영계 전용 지형과 포털 미리보기는 후속 범위다.' 462

    $s=NewSlide '현재 시연의 진행 흐름' '기획서 4장. PassGameFlow.cs, PassShrineInteraction.cs, PassBossEntrance.cs의 현재 연결을 확인했다. 기도는 회복과 체크포인트를 제공하지만 산문 통과의 강제 선행 조건은 아니다. 시작 화면의 보스 즉시 도전은 일반 산길 구간을 건너뛴다.'
    Table $s @(@('구간','플레이어 행동','경험과 시스템 반응'),@('01  산길 시작','등불을 따라 오르기','목표와 거리 안내, 안개 속 접근'),@('02  산신각','F로 기도하기','체력 회복, 호리병 충전, 거점 갱신'),@('03  영적 산문','문을 통과하기','환경 전환, 보스 등장, 퇴로 차단'),@('04  수문장 전투','회피와 부채 연격','전조를 읽고 후딜레이에 반격'),@('05  결과·재도전','승패 확인 후 재시작','거점 재도전 또는 산길 처음부터')) 68 134 824 287 @(160,228,436) 16.5
    Text $s '빠른 시연은 시작 화면의 「수문장에게 도전」으로 바로 보스전에 진입' 68 441 824 35 18 $script:jade

    $s=NewSlide '산길 탐색과 산신각' '기획서 3~4장, 8장, 10장. PassStairAtmosphere.cs와 PassMountainMist.cs가 등반 높이와 분위기를 제어하고 PassShrineInteraction.cs가 기도 및 거점 기능을 담당한다. 기도 기본 시간은 약 1.2초다. 이미지: Assets/Temp/SummitMistReveal~/Start.png.'
    HeadingBody $s '등불이 이끄는 오르막' "계단과 목적지 표시로 길을 안내한다.`n높이에 따라 밝기와 안개를 바꾼다." 68 141 347 75
    HeadingBody $s '수련의 거점' "산신각에서 약 1.2초 동안 기도한다.`n회복과 체크포인트 갱신을 함께 제공한다." 68 283 347 90
    Photo $s 'Assets/Temp/SummitMistReveal~/Start.png' 449 139 443 295 '산길 시작 구간 · 프로젝트 보관 캡처'
    SmallNote $s '낙하 시 마지막 체크포인트로 복귀하며 기본 피해 20을 받는다.'

    $s=NewSlide '조작과 카메라' '기획서 5장. 현재 키 입력은 PassPlayerMovement.cs, PassAnimationController.cs, PassCamera.cs, PassGourdDrinking.cs, PassGameFlow.cs, PassBossEntrance.cs와 비교했다. E 회복은 현재 프로젝트에서 추가된 기능이다. 숫자 1~3은 동작 요청이며 현재 전환 조건을 따른다. Space/Esc는 보스 등장 연출 중 건너뛰기로도 쓰인다.'
    Table $s @(@('입력','이동·시점'),@('WASD / 방향키','카메라 기준 이동'),@('Shift + 이동','달리기'),@('마우스 / 휠','시점 회전 / 거리 조절'),@('Tab','보스 주시 전환'),@('Space','구르기 회피')) 68 135 394 276 @(175,219) 17
    Table $s @(@('입력','전투·상호작용'),@('좌클릭 / J','발도 및 순차 연격'),@('1 / 2 / 3','해당 공격 요청'),@('F / E','산신각 기도 / 호리병'),@('Esc','일시정지 / 복귀'),@('R','승패 후 거점 재시작')) 498 135 394 276 @(145,249) 17
    SmallNote $s '보스전 시작 시 주시 활성화 · 지형 충돌로 카메라 가림 보정 · 등장 연출은 Space/Esc로 건너뛰기' 446

    $s=NewSlide '부채 3단 연격' '기획서 6장과 현재 PassFanAttackCombat.cs, PassFanAttackVfx.cs, MountainFanAttackVfx 설정. 1타와 3타에 직접·바람 피해가 있고 2타는 직접 타격 중심이다. 기준 피해는 각각 22, 28, 40. 아래 그림은 현재 코드를 실행해 새로 촬영한 것이 아닌 보관된 효과 캡처다. 출처: Assets/Temp/WindWaveIntegration~/WindWave_Attack1.png, WindWave_Attack2.png, WindWave_Attack3.png.'
    Photo $s 'Assets/Temp/WindWaveIntegration~/WindWave_Attack1.png' 68 132 256 198 '' @(260,100,650,550)
    Photo $s 'Assets/Temp/WindWaveIntegration~/WindWave_Attack2.png' 352 132 256 198 '' @(280,215,570,440)
    Photo $s 'Assets/Temp/WindWaveIntegration~/WindWave_Attack3.png' 636 132 256 198 '' @(220,200,600,470)
    Text $s '1타  세로 내려치기' 68 351 256 32 20 $script:jade $true
    Text $s '2타  가로 휘두르기' 352 351 256 32 20 $script:jade $true
    Text $s '3타  넓은 마무리' 636 351 256 32 20 $script:jade $true
    Text $s "직접 타격과 전방 바람`n기준 피해 22" 68 395 256 66 18
    Text $s "직접 타격 중심`n기준 피해 28" 352 395 256 66 18
    Text $s "넓은 궤적과 전방 바람`n기준 피해 40" 636 395 256 66 18
    SmallNote $s '효과는 프로젝트 보관 캡처 · 피해량은 현재 씬의 작업값' 479

    $s=NewSlide '직접 타격과 바람 피해' '현재 MountainPass_Optimized.unity의 PassFanAttackDamageSettings 값과 PassFanAttackCombat.cs를 기준으로 한다. attack1 baseDamage22 directWeight0.7 windWeight0.3, attack2 baseDamage28 directWeight1, attack3 baseDamage40 directWeight0.7 windWeight0.3. 표의 직접·바람 피해는 기준 피해량과 비율을 곱해 계산했다. 코드상 같은 공격·동일 대상에 채널별 1회 적용하며 실제 충돌 조건 충족 여부에 따라 결과가 달라진다. 개정본 6.3장의 별도 테스트 구현은 현재 씬으로 통합된 상태로 갱신했다.'
    Text $s '같은 연격 안에서 근접 부채와 원격 바람을 별도로 판정' 68 130 824 48 23 $script:jade
    Table $s @(@('공격','기준 피해','직접 피해','바람 피해'),@('1타','22','15.4  (70%)','6.6  (30%)'),@('2타','28','28  (100%)','없음'),@('3타','40','28  (70%)','12  (30%)')) 68 201 824 190 @(170,170,242,242) 20
    Text $s '동일 공격의 같은 대상은 직접·바람 채널별로 한 번씩 명중' 68 414 824 36 18.5
    SmallNote $s '두 채널이 모두 맞을 때 기준 피해량에 도달한다. 수치는 최종 밸런스 확정값이 아니다.' 462

    $s=NewSlide '회피와 피격의 전투 흐름' '기획서 5.3, 6.2, 8장. 현재 PassAnimationController.cs는 HEAVY_HIT, GET_UP, DEATH를 처리하고 PassGameFlow.cs는 강피격 및 치명 피해를 실제 상태와 연결한다. inputBufferSeconds는 현재 씬에서 0.15, 플레이어 hitRecovery는 0.65. 현재 씬에서 연결된 MountainActionTimings 및 MountainPlayerReactions 설정을 따른다. 특정 구르기 무적 비율은 공격 종류와 판정 구현에 따라 달라질 수 있어 단일 확정 규칙으로 표시하지 않았다.'
    HeadingBody $s '전조 확인과 구르기' '적의 준비 동작과 범위를 보고 피한 뒤, 회복 중인 보스를 공격한다.' 68 139 824 52
    HeadingBody $s '입력 유예와 캔슬' '전환 직전의 입력을 약 0.15초 받아 두고 동작별 허용 시점에 실행한다.' 68 248 824 52
    HeadingBody $s '일반 피격과 강피격' '강한 공격은 넘어짐과 기상으로 연결한다. 치명 피해는 사망 상태로 이어진다.' 68 357 824 60
    SmallNote $s '체력 100 · 피해 후 기본 재피격 유예 0.65초 · 캔슬과 무적 구간은 동작별 설정을 따른다.'

    $s=NewSlide '호리병 회복과 재도전' '개정본 8장 이후의 프로젝트 추가 내용을 반영했다. MountainPass_Optimized.unity의 PassGourdDrinking: maximumUses5, startingUses3, healAmount40. E 입력으로 사용하며 이동을 유지하는 상반신 레이어를 사용한다. PassGameFlow.RestAtShrine는 체력 회복과 Refill을 호출한다. 전투 중에는 산신각 기도를 사용할 수 없고, 호리병은 피격·공격 등 상태 전환에 의해 취소될 수 있다. 출처 이미지: Assets/Temp/MainSceneIntegration~/T_MainDrinking.png.'
    Text $s '40' 68 137 156 73 47 $script:jade $false $script:titleFont
    Text $s '1회 회복량' 71 219 165 28 17 $script:muted
    HeadingBody $s 'E로 회복' '이동을 유지하며 호리병을 마신다.' 68 277 315 64
    Text $s "시작 3회 · 최대 5회`n산신각 기도로 충전`n피격·공격 전환 시 취소 가능" 68 377 320 92 18.5
    Photo $s 'Assets/Temp/MainSceneIntegration~/T_MainDrinking.png' 426 141 466 296 '호리병 사용 동작 · 프로젝트 보관 캡처'

    $s=NewSlide '장승 수문장 보스' '기획서 7장과 현재 PassBossPatternController.cs. 표시 이름 천하대장군 · 산문의 수문장. 현재 확장 컨트롤러가 본편 및 최적화 씬에 연결되어 있다. 체력은 설정상 본체420, 미니60. 확장 컨트롤러는 Enraged를 false로 재정의하므로 개정본의 구형 50% 격노 규칙을 현행 기본 규칙으로 제시하지 않는다. 사진: Assets/Temp/PursuitSlamBuild~/Test_NoseSteam.png.'
    Text $s '천하대장군' 68 143 350 47 30 $script:jade $false $script:titleFont
    Text $s '산문의 수문장' 68 199 350 33 20 $script:gold
    Text $s "견습 도사의 첫 관문`n공격 전조와 회피 시점 학습`n후딜레이의 반격 기회" 68 259 352 111 21
    Text $s '본체 체력 420' 68 406 346 40 25 $script:jade $true
    Photo $s 'Assets/Temp/PursuitSlamBuild~/Test_NoseSteam.png' 459 131 433 326 '수문장과 코김 전조 · 프로젝트 보관 캡처'

    $s=NewSlide '수문장의 주요 공격과 대응' '현재 PassBossPatternController.cs, PassBossPatternSettings.cs, 최적화 씬의 settings 값을 근거로 구성했다. 점프 본체 피해20, 엎어지기28, crack 피해18 및 stepCount5. 추적 찍기는 별도 슬라이드에서 자세히 설명한다. 공격 후 이동·구르기·반격의 역할은 기획서 2장과 7장의 전투 원칙을 따른다. 표시 피해는 현재 작업값이며 플레이 밸런스 검증 완료를 의미하지 않는다.'
    Table $s @(@('패턴','공격 특징','대응과 반격'),@('이동 점프','플레이어 주변으로 뛰어 착지 공격','착지 위치를 보고 이탈'),@('엎어지기','몸체를 넘어뜨려 앞쪽을 타격','측면 회피 후 넘어진 동안 공격'),@('지면 연속 폭발','전방 5개 지점을 순차 타격','직선 경로에서 벗어나기'),@('추적 점프·등 찍기','추적 점프 후 큰 강하 공격','연속 회피 후 긴 후딜레이 활용')) 68 138 824 271 @(188,318,318) 17.5
    SmallNote $s '본체 작업값: 점프 피해 20 · 엎어지기 피해 28 · 지면 폭발 피해 18' 445

    $s=NewSlide '추적 점프와 등 찍기' '현재 PassBossPursuitSlam.cs와 MountainPass_Optimized.unity pursuitSlam 설정. windup1.5, trackingJumps2, trackingDamage20, heavyDamage28, bodyDamageMultiplier1.5, recovery7. 각 이륙 직전 플레이어 위치를 기준으로 유효 지면을 찾아 착지점을 확정한다. 최종 직접 충돌과 충격파를 중복 적용하지 않는다. 직접 몸체 피격42는 28×1.5로 계산한 값이다. 사진: Assets/Temp/PursuitSlamBuild~/Test_BackSlamDust.png.'
    Photo $s 'Assets/Temp/PursuitSlamBuild~/Test_BackSlamDust.png' 68 139 452 291 '최종 강하 후 회복 구간 · 프로젝트 보관 캡처'
    Text $s "코김 전조 1.5초`n`n추적 점프 2회`n`n높이 뛰어 등으로 찍기`n`n회복 7초 동안 반격" 556 139 336 280 22 $script:jade
    SmallNote $s '현재 작업값: 추적 피해 20 · 최종 충격파 28 · 몸체 직접 충돌 42 (중복 적용 없음)' 463

    $s=NewSlide '미니 장승 소환과 시간차 연계' '현재 최적화 씬 summon 및 linkedAttack 설정, PassBossPatternController.cs 및 PassBossLinkedAttack.cs를 근거로 한다. summon.requiredNormalAttacks3, spawnCount2, maximumAlive2, combat.minionHealth60. 연계 간격은 일반 공격2~3회, 참여2체, 순간이동 연기와 시간차 타격을 사용한다. 구형 테스트의 이동 점프3회 누적 조건과 현재 일반 공격3회 조건을 구분했다. 사진: Assets/Temp/BossPatterns~/Test_LinkTeleportArrival.png.'
    Photo $s 'Assets/Temp/BossPatterns~/Test_LinkTeleportArrival.png' 68 139 463 289 '본체와 미니 장승의 합동 공격 · 과거 테스트 캡처'
    HeadingBody $s '일반 공격 누적 후 소환' "3회 수행 후 미니 장승 소환`n한 번에 2체 · 최대 생존 2체" 565 140 327 77
    HeadingBody $s '본체와 미니 장승의 연계' "순간이동으로 자리를 잡고`n점프와 본체 타격을 시간차로 연결" 565 284 327 87
    SmallNote $s '미니 장승 체력 60 · 본체 처치 시 남은 미니 장승도 정리하고 소멸 효과 처리' 463

    $s=NewSlide 'UI와 시연 편의 기능' '기획서 9장과 PassHud.cs, PassGameFlow.cs의 현재 상태. uGUI와 한국어 TextMeshPro 기반. 호리병 잔여 횟수와 회복량은 현행 UI 추가 사항이다. 구형 보스 격노 문구는 현행 확장 컨트롤러의 격노 비활성화와 구분해야 한다. 인벤토리·퀘스트 목록·성장 트리·미니맵은 이번 시연의 범위에 포함하지 않는다.'
    Table $s @(@('플레이 상황','화면이 전달할 정보'),@('시작','산길 여정과 보스 즉시 도전 선택'),@('탐색','목표, 목적지 방향·거리, 기도 상호작용'),@('전투','플레이어·보스 체력, 호리병 잔여 횟수'),@('등장 연출·일시정지','건너뛰기, 계속하기와 재시작'),@('승리·패배','결과와 재도전, 승리 시 경과 시간·명중 횟수')) 68 137 824 285 @(233,591) 18
    SmallNote $s '한지색·옥색·금색을 사용한 화면 구성 · 장비창과 성장 UI는 후속 기획' 459

    $s=NewSlide '아트와 사운드 방향' '기획서 10장. 한국 전통 건축과 산악 자연, 스타일라이즈드 캐릭터·장승을 사용한다. 현재 환경·전투 효과 코드는 본편으로 통합되어 있으며 미래 음악 방향과는 분리한다. PassAudio.cs는 부채·피격·충격·종·산바람을 코드로 합성한다. 전통 악기 기반 BGM 및 최종 믹싱은 후속 작업이다. 이미지: Assets/Temp/SummitMistReveal~/Opening.png. 자산 보유는 실제 씬 사용과 동일하지 않다.'
    HeadingBody $s '한국풍 3D 카툰 렌더링' "바위산과 소나무, 돌계단과 등불`n산신각과 산문이 이어지는 수련지" 68 137 360 78
    HeadingBody $s '환경과 전투 연출' "높이에 따라 걷히는 안개`n부채 궤적, 바람, 충격과 소멸" 68 266 360 74
    Text $s "현재  합성 효과음과 산바람`n후속  전통 악기 기반 BGM·믹싱" 68 403 360 69 17.5 $script:jade
    Photo $s 'Assets/Temp/SummitMistReveal~/Opening.png' 461 139 431 292 '등불과 계단의 시선 유도 · 프로젝트 보관 캡처'

    $s=NewSlide '개정본 이후의 프로젝트 현황' '2026-10-26 개정본과 2026-10-27 저장 상태 비교다. 현재 빌드 활성 씬은 ProjectSettings/EditorBuildSettings.asset의 MountainPass_Optimized.unity. 현재 최적화 씬에는 PassFanAttackCombat, PassBossPatternController, PassAnimationController.reactionSettings, PassGourdDrinking 및 PassGameFlow.gourd가 활성 참조로 연결되어 있다. 따라서 개정본의 별도 테스트 구분 및 SampleScene 빌드 등록 설명은 현행 파일과 차이가 있다. 모든 상태는 저장된 파일 근거이며 이번 작업에서 실행 검증은 하지 않았다.'
    Table $s @(@('영역','개정본 v0.2','현재 저장된 프로젝트'),@('부채 VFX·바람 피해','별도 테스트','본편·최적화 씬 연결'),@('확장 보스 패턴','별도 테스트','본편·최적화 씬 연결'),@('강피격·기상·사망','후속 통합','실제 피격·패배 상태 연결'),@('호리병 회복','상세 기재 없음','E 회복·산신각 충전 연결'),@('활성 빌드 씬','SampleScene','MountainPass_Optimized')) 68 135 824 290 @(215,225,384) 17
    SmallNote $s '「씬 연결」은 저장된 코드·참조 확인을 뜻한다. 실행 안정성·배포 완성·성능 검증과 구분한다.' 459

    $s=NewSlide '1차 시연 완료 기준' '기획서 12.1장의 완료 기준을 현행 통합 상태에 맞춰 정리했다. 본편의 통합 연결 자체를 재작업 항목으로 제시하지 않고 정상적인 사용자 흐름 및 실행본 검증을 완료 조건으로 삼았다. 현재 빌드 등록은 되어 있으나 이번 읽기 전용 확인에서는 빌드와 플레이 실행을 하지 않았다.'
    Text $s '하나의 실행본에서 시작부터 재도전까지 이어지는 상태' 68 133 824 43 24 $script:jade
    Table $s @(@('확인 구간','완료 조건'),@('진입과 탐색','일반 여정과 보스 즉시 도전, 막힘 없는 이동'),@('거점과 전환','기도·호리병 충전, 산문 통과와 등장 연출 연결'),@('전투','보이는 공격과 피해 일치, 회피·반격 기회 확보'),@('반복 플레이','승리·패배·낙하·일시정지 후 정상 재도전'),@('배포','등록된 최적화 씬으로 빌드 후 같은 흐름 확인')) 68 202 824 239 @(208,616) 16.5
    SmallNote $s '시연 분량과 잔여 일정은 통합된 흐름을 실제로 플레이한 뒤 다시 산정한다.' 464

    $s=NewSlide '후속 작업의 우선순위' '기획서 12.2장의 의도를 유지하되 현재 완료된 통합 연결을 고려해 제안한 후속 순서다. 이 순서는 새 일정 약속이나 구현 확정이 아닌 기획상 우선순위 제안이다. 첫째 통합 전투 판정·패턴 밸런스, 둘째 반복 플레이와 가독성, 셋째 배포 실행 및 실제 성능, 넷째 음악과 추가 도술 검토. 21일·하루4~5시간은 초안의 최초 목표이므로 잔여 일정으로 재사용하지 않았다.'
    Text $s '통합 이후의 검증과 조정에 집중' 68 133 824 45 27 $script:jade $false $script:titleFont
    Table $s @(@('순서','작업','확인할 결과'),@('1','공격 판정·보스 난이도 조정','VFX와 피해 일치, 전조·반격 시간'),@('2','반복 플레이·가독성 점검','재도전 안정성, 시야와 입력 응답'),@('3','배포 실행·성능 측정','실행본 동작, 프레임과 메모리'),@('4','사운드·추가 도술 검토','음악 교체, 역할과 자원 규칙')) 68 205 824 234 @(84,338,402) 17
    SmallNote $s '현황을 반영한 우선순위 제안 · 60FPS 제한 설정은 실제 성능 유지의 검증 결과가 아니다.' 465

    $s=NewSlide '장기 확장 방향' '기획서 13장과 6.4장. 독립 도술 슬롯·마력·쿨타임 시스템, 성장·퀘스트·수집과 다른 지역은 후속 기획이다. 현재 부채 전투와 영적 전환 경험을 먼저 안정화한 다음 확장한다. 출시 플랫폼과 저장·진행 구조는 미정이다. 아래 내용은 구현 완료 목록이 아니다.'
    HeadingBody $s '도술과 전투의 확장' '부채와 바람을 발전시키고, 독립 도술의 역할과 자원·재사용 규칙을 정한다.' 68 139 824 60
    HeadingBody $s '현실과 영계의 차이 확대' '같은 장소의 환경 변화에서 나아가 지형·기믹·적 배치의 차이를 만든다.' 68 250 824 60
    HeadingBody $s '다음 수련과 성장' '한국 설화 기반 지역과 보스를 추가하고 탐험·수집·성장·퀘스트를 도입한다.' 68 361 824 60
    SmallNote $s '현재 한 구간의 경험을 안정화한 뒤 확장 · 출시 플랫폼과 저장 구조는 별도 결정'

    $s=NewSlide '부록 · 자료와 확인 범위' '기획 자료 전체: E:/C.J.I/Document/천지인 기획서 개정본.md. 이미지 테마: C:/Users/MBC/Downloads/cheonjiin-overview-background-v2.png. 저장 현황 확인 대상: D:/project/ProjectCheon/Assets/Scenes/MountainPass/MountainPass.unity 및 MountainPass_Optimized.unity; Assets/Scripts/MountainPass; Assets/Settings/MountainPass; Assets/Prefabs/MountainPass; ProjectSettings/EditorBuildSettings.asset; ProjectSettings/ProjectVersion.txt; Packages/manifest.json. 그림 출처는 각 슬라이드 노트에 기록했다. 읽기 전용 조사로 프로젝트의 씬·스크립트·설정 및 원본 문서·이미지는 변경하지 않았다. 보관 캡처는 새 실행 증거가 아니다.'
    HeadingBody $s '기획 기준' "천지인 기획서 개정본 v0.2`n2026년 10월 26일 개정" 68 137 369 76
    HeadingBody $s '프로젝트 기준' "2026년 10월 27일 저장 상태`nMountainPass / MountainPass_Optimized" 498 137 394 76
    Text $s '주요 확인 자료' 68 278 824 32 21 $script:jade $true
    Text $s "씬·프리팹의 컴포넌트와 참조`n진행·전투·회복·보스·카메라 코드 및 설정값`nUnity 버전·패키지·활성 빌드 씬" 68 324 824 107 19
    SmallNote $s '플레이 실행·배포 빌드·실측 성능은 이번 확인에 포함하지 않았다. 상세 경로는 슬라이드 노트에 기록했다.' 463

    if($script:deck.Slides.Count -ne 22){throw 'Expected 22 slides'}
    $draftPath="$workRoot/build/Cheonjiin_${Revision}_draft.pptx"
    $finalPath="$outputRoot/천지인_기획서_슬라이드_20261027_${Revision}.pptx"
    $pdfPath="$outputRoot/천지인_기획서_슬라이드_20261027_${Revision}.pdf"
    if((Test-Path -LiteralPath $finalPath) -or (Test-Path -LiteralPath $pdfPath)){throw 'Output already exists'}
    $script:deck.SaveAs($draftPath,24)
    $fitIssues=[System.Collections.Generic.List[object]]::new()
    foreach($slide in $script:deck.Slides){
        foreach($shape in $slide.Shapes){
            if($shape.HasTextFrame -eq -1 -and $shape.TextFrame.HasText -eq -1){
                $bound=$shape.TextFrame.TextRange.BoundHeight
                if($bound -gt $shape.Height+1){$fitIssues.Add(@{slide=$slide.SlideIndex;name=$shape.Name;boundHeight=$bound;height=$shape.Height;text=$shape.TextFrame.TextRange.Text})}
            }
            if($shape.HasTable -eq -1){
                for($r=1;$r -le $shape.Table.Rows.Count;$r++){
                    for($c=1;$c -le $shape.Table.Columns.Count;$c++){
                        $cellShape=$shape.Table.Cell($r,$c).Shape
                        $available=$cellShape.Height-$cellShape.TextFrame.MarginTop-$cellShape.TextFrame.MarginBottom
                        $bound=$cellShape.TextFrame.TextRange.BoundHeight
                        if($bound -gt $available+1){$fitIssues.Add(@{slide=$slide.SlideIndex;row=$r;column=$c;boundHeight=$bound;height=$available;text=$cellShape.TextFrame.TextRange.Text})}
                    }
                }
            }
        }
        $previewPath=[System.IO.Path]::GetFullPath("$workRoot/previews/slide_$('{0:00}' -f $slide.SlideIndex).png")
        $slide.Export($previewPath,'PNG',1440,810)
    }
    $fitIssues | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$workRoot/build/fit_${Revision}.json" -Encoding utf8
    $script:textLog | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$workRoot/build/text_${Revision}.json" -Encoding utf8
    if($fitIssues.Count -gt 0){throw "Text fit issues: $($fitIssues.Count). Inspect private fit report."}
    $script:deck.SaveAs($finalPath,24)
    $script:deck.SaveAs([System.IO.Path]::GetFullPath($pdfPath),32)
    $report=@{slides=$script:deck.Slides.Count;sizePoints=@(960,540);fonts=@($script:titleFont,$script:bodyFont);fitIssues=$fitIssues.Count;pptx=$finalPath;pdf=$pdfPath;authoring='Native PowerPoint automation. Artifact runtime unavailable in this host.';review='Source read-only; pre-rendered captures used with provenance in notes.'}
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$workRoot/build/report_${Revision}.json" -Encoding utf8
    Write-Output ($report | ConvertTo-Json -Depth 6)
} finally {
    if($null -ne $script:deck){$script:deck.Close(); [Runtime.InteropServices.Marshal]::ReleaseComObject($script:deck) | Out-Null}
    if($null -ne $script:app){$script:app.Quit(); [Runtime.InteropServices.Marshal]::ReleaseComObject($script:app) | Out-Null}
}
