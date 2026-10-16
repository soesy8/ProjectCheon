$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot = 'D:\project\ProjectCheon\Assets\Temp\BlenderReferenceAlignment'
$frontPath = 'E:\C.J.I\ReferenceImage\PlayerCharacter\T_PlayerCharacter_Front.png'
$frontImage = [Drawing.Image]::FromFile($frontPath)
$sideImage = [Drawing.Image]::FromFile((Join-Path $taskRoot 'T_PlayerCharacter_Side.png'))

# 비교용 그림은 기준선이 원본에 섞이지 않도록 별도 미리보기로만 저장한다.
$preview = New-Object Drawing.Bitmap 2304, 1120
$graphics = [Drawing.Graphics]::FromImage($preview)
$graphics.Clear([Drawing.Color]::FromArgb(245, 245, 245))
$graphics.DrawImage($frontImage, [Drawing.Rectangle]::new(96, 48, 1024, 1024))
$graphics.DrawImage($sideImage, [Drawing.Rectangle]::new(1184, 48, 1024, 1024))
$font = New-Object Drawing.Font 'Arial', 17
$smallFont = New-Object Drawing.Font 'Arial', 11
$graphics.DrawString('FRONT / MASTER', $font, [Drawing.Brushes]::Black, 96, 12)
$graphics.DrawString('SIDE / PIXEL ALIGNED', $font, [Drawing.Brushes]::Black, 1184, 12)
$guideColor = [Drawing.Color]::FromArgb(150, 200, 40, 75)
$pen = New-Object Drawing.Pen $guideColor, 1
$guideBrush = New-Object Drawing.SolidBrush $guideColor
$landmarks = @(
    @{ Name = 'Hat'; Y = 7 },
    @{ Name = 'Chin'; Y = 303 },
    @{ Name = 'Shoulder'; Y = 354 },
    @{ Name = 'Belt'; Y = 480 },
    @{ Name = 'Belt end'; Y = 509 },
    @{ Name = 'Bag'; Y = 637 },
    @{ Name = 'Fingers'; Y = 668 },
    @{ Name = 'Hem'; Y = 824 },
    @{ Name = 'Ankle'; Y = 916 },
    @{ Name = 'Sole'; Y = 988 }
)
foreach ($landmark in $landmarks) {
    $guideY = [int]$landmark.Y + 48
    $graphics.DrawLine($pen, 96, $guideY, 2208, $guideY)
    $graphics.DrawString($landmark.Name, $smallFont, $guideBrush, 8, $guideY - 8)
}
$graphics.Dispose()
$preview.Save((Join-Path $taskRoot 'T_ReferenceAlignmentPreview.png'), [Drawing.Imaging.ImageFormat]::Png)
$pen.Dispose()
$guideBrush.Dispose()
$font.Dispose()
$smallFont.Dispose()
$preview.Dispose()
$frontImage.Dispose()
$sideImage.Dispose()
Write-Output 'Saved 1024px reference and separate comparison preview.'
