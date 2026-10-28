Add-Type -AssemblyName System.Drawing
$stairCloudBefore = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'ClimbingFinal.png'))
$stairCloudAfter = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'ClimbingWithoutCloud.png'))
$stairCloudAreas = @{
    center = @(410,230,614,346)
    gourd_opaque_icon = @(49,504,58,526)
    health_fill = @(100,38,240,43)
    left_cloud = @(10,170,190,260)
}
$stairCloudMetrics = @{}
foreach ($stairCloudEntry in $stairCloudAreas.GetEnumerator()) {
    $stairCloudBox = $stairCloudEntry.Value
    $stairCloudTotal = 0.0
    $stairCloudMax = 0
    $stairCloudCount = 0
    for ($stairCloudY = $stairCloudBox[1]; $stairCloudY -lt $stairCloudBox[3]; $stairCloudY++) {
        for ($stairCloudX = $stairCloudBox[0]; $stairCloudX -lt $stairCloudBox[2]; $stairCloudX++) {
            $stairCloudPixel1 = $stairCloudBefore.GetPixel($stairCloudX,$stairCloudY)
            $stairCloudPixel2 = $stairCloudAfter.GetPixel($stairCloudX,$stairCloudY)
            foreach ($stairCloudChannel in @('R','G','B')) {
                $stairCloudDelta = [Math]::Abs([int]$stairCloudPixel1.$stairCloudChannel - [int]$stairCloudPixel2.$stairCloudChannel)
                $stairCloudTotal += $stairCloudDelta
                $stairCloudMax = [Math]::Max($stairCloudMax,$stairCloudDelta)
                $stairCloudCount++
            }
        }
    }
    $stairCloudMetrics[$stairCloudEntry.Name] = @{ mean_rgb_difference = $stairCloudTotal / $stairCloudCount; maximum_rgb_difference = $stairCloudMax }
}
$stairCloudBefore.Dispose()
$stairCloudAfter.Dispose()
$stairCloudMetrics | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'PixelComparison.json') -Encoding UTF8
$stairCloudMetrics | ConvertTo-Json
