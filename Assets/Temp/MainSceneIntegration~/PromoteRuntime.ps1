$ErrorActionPreference = 'Stop'
$root = 'D:/project/ProjectCheon'
$encoding = New-Object System.Text.UTF8Encoding($false)
$names = @('FanAttackVfx', 'FanAttackVfxProfile', 'FanSweepMesh', 'FanCloudSpiralVfx', 'FanWindParticles', 'RollDust', 'RollDustProfile', 'FanAttackDamageSettings', 'FanAttackHitDetection')
foreach ($name in $names) {
    $source = "$root/Assets/Test/FanAttackVfx/Scripts/Test_$name.cs"
    $targetName = if ($name -eq 'FanAttackHitDetection') { 'PassFanAttackCombat' } else { "Pass$name" }
    $destination = "$root/Assets/Scripts/MountainPass/$targetName.cs"
    if ([System.IO.File]::Exists($destination)) { throw "Already exists: $destination" }
    $content = [System.IO.File]::ReadAllText($source).Replace('Cheon.Tests.FanAttackVfx', 'Cheon.Prototype.MountainPass').Replace('Test_FanAttackHitDetection', 'PassFanAttackCombat').Replace('Test_', 'Pass').Replace('Cheon/Test/', 'Cheon/Mountain Pass/')
    [System.IO.File]::WriteAllText($destination, $content, $encoding)
}
foreach ($name in @('GourdAttachment', 'GourdDrinkingPose')) {
    $content = [System.IO.File]::ReadAllText("$root/Assets/Test/PlayerReactions/Drinking/Scripts/Test_$name.cs").Replace('Cheon.Tests.PlayerReactions', 'Cheon.Prototype.MountainPass').Replace('Test_PlayerReactionPreview', 'PassAnimationController').Replace('Test_', 'Pass')
    [System.IO.File]::WriteAllText("$root/Assets/Scripts/MountainPass/Pass$name.cs", $content, $encoding)
}
