$ErrorActionPreference='Stop'
$labels=@{}
Get-Content -Encoding UTF8 'Assets/Temp/SceneOptimization~/InspectorLabels.txt' | ForEach-Object { $pair=$_ -split '\|',2; if($pair.Count -eq 2){$labels[$pair[0]]=$pair[1]} }
$fields=Get-Content -Raw -Encoding UTF8 'Assets/Temp/SceneOptimization~/InspectorFields.json' | ConvertFrom-Json
$count=0
foreach($group in ($fields | Where-Object { !$_.label } | Group-Object path)) {
    $wanted=@{}
    foreach($field in $group.Group){if(!$labels.ContainsKey($field.name)){throw ('Missing label '+$field.name)}; $wanted[$field.name]=$true}
    $absolute=Join-Path 'D:/project/ProjectCheon' $group.Name
    $lines=[IO.File]::ReadAllLines($absolute)
    $output=New-Object 'Collections.Generic.List[string]'
    foreach($line in $lines) {
        $match=[regex]::Match($line,'(?:public|private|protected)\s+[\w.<>]+(?:\[\])?\s+(?<name>[A-Za-z_]\w*)\s*(?=;|=(?!>))')
        if($match.Success -and $wanted.ContainsKey($match.Groups['name'].Value) -and !$line.Contains('InspectorName(')) {
            $name=$match.Groups['name'].Value
            $indent=[regex]::Match($line,'^\s*').Value
            $output.Add($indent+'[InspectorName("'+$labels[$name]+'")]')
            $count++
        }
        $output.Add($line)
    }
    [IO.File]::WriteAllText($absolute,([string]::Join("`n",$output)+"`n"),(New-Object Text.UTF8Encoding($false)))
}
Write-Output ('Translated field declarations: '+$count)
