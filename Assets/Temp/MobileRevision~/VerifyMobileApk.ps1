$ErrorActionPreference = 'Stop'
$projectRoot = 'D:\project\ProjectCheon'
$reportFolder = Join-Path $projectRoot 'Assets\Temp\MobileRevision~'
$apkPath = 'E:\C.J.I\APKBuild\천지인.apk'
$androidPlayer = 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Data\PlaybackEngines\AndroidPlayer'
$buildTools = Join-Path $androidPlayer 'SDK\build-tools\36.0.0'
$badging = @(& (Join-Path $buildTools 'aapt2.exe') dump badging $apkPath)
if ($LASTEXITCODE -ne 0) { throw 'APK badging failed.' }
$manifest = @(& (Join-Path $buildTools 'aapt2.exe') dump xmltree --file AndroidManifest.xml $apkPath)
if ($LASTEXITCODE -ne 0) { throw 'Manifest inspection failed.' }
$resources = @(& (Join-Path $buildTools 'aapt2.exe') dump resources $apkPath)
if ($LASTEXITCODE -ne 0) { throw 'Resource inspection failed.' }
if (-not ($badging -match "application-label:'천지인'")) { throw 'Unexpected app label.' }
if (-not ($badging -match "versionCode='2'")) { throw 'Unexpected version code.' }
if (-not ($badging -match "native-code: 'arm64-v8a'")) { throw 'Unexpected architecture.' }
if (-not ($manifest -match 'android:screenOrientation.*=(?:0x0+|0)\s*$')) { throw 'Landscape orientation missing.' }
$signing = @(& (Join-Path $androidPlayer 'OpenJDK\bin\java.exe') -jar (Join-Path $buildTools 'lib\apksigner.jar') verify --verbose $apkPath)
if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed.' }
$alignment = @(& (Join-Path $buildTools 'zipalign.exe') -c -P 16 4 $apkPath)
if ($LASTEXITCODE -ne 0) { throw 'APK alignment verification failed.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($apkPath)
$extracted = @()
try {
    foreach ($pair in @(@('mipmap/app_icon','T_ApkLauncherIcon.png'), @('mipmap/ic_launcher_foreground','T_ApkAdaptiveForeground.png'))) {
        $reading = $false
        $resourcePath = $null
        foreach ($line in $resources) {
            if ($line -match '^\s*resource\s+\S+\s+(\S+)$') { $reading = $Matches[1] -eq $pair[0] }
            if ($reading -and $line -match '\(xxxhdpi\) \(file\) (\S+\.png)') { $resourcePath = $Matches[1]; break }
        }
        if ($null -eq $resourcePath) { throw ('Missing resource mapping: ' + $pair[0]) }
        $entry = $archive.GetEntry($resourcePath)
        if ($null -eq $entry) { throw ('Missing packaged icon: ' + $pair[0]) }
        $destination = Join-Path $reportFolder $pair[1]
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $true)
        $extracted += [pscustomobject]@{ resource = $entry.FullName; preview = $destination }
    }
} finally { $archive.Dispose() }

$images = Get-Content -Raw -Encoding UTF8 (Join-Path $reportFolder 'ImageVerification.json') | ConvertFrom-Json
$touchChecks = Get-Content -Raw -Encoding UTF8 (Join-Path $reportFolder 'TouchVerification.json') | ConvertFrom-Json
if (@($images | Where-Object { $_.width -ne 512 -or $_.height -ne 512 }).Count) { throw 'A source image is not 512x512.' }
if (@($touchChecks | Where-Object { -not $_.passed }).Count) { throw 'Touch verification has failed checks.' }
$file = Get-Item -LiteralPath $apkPath
$report = [pscustomobject]@{
    apk = $file.FullName
    bytes = $file.Length
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $apkPath).Hash
    displayName = '천지인'
    versionCode = 2
    orientation = 'LandscapeLeft'
    architecture = 'arm64-v8a'
    source512Images = $images.Count
    passedEditorTouchAndLayoutChecks = $touchChecks.Count
    editorTestResolution = '2340x1080'
    signature = $signing
    alignmentPassed = $true
    packagedIconPreviews = $extracted
    verifiedAt = (Get-Date -Format o)
}
$report | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $reportFolder 'ApkVerification.json')
$report | ConvertTo-Json -Depth 6
