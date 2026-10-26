$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

Add-Type -TypeDefinition @'
using System;
using System.IO;
public static class Test_CloudComboGifTiming
{
    public static int Apply(string path)
    {
        byte[] source = File.ReadAllBytes(path);
        int position = 13;
        if ((source[10] & 128) != 0) position += 3 * (1 << ((source[10] & 7) + 1));
        using (var result = new MemoryStream())
        {
            result.Write(source, 0, position);
            byte[] looping = {33,255,11,78,69,84,83,67,65,80,69,50,46,48,3,1,0,0,0};
            result.Write(looping, 0, looping.Length);
            int frames = 0;
            while (position < source.Length)
            {
                int start = position;
                int marker = source[position++];
                if (marker == 59) { result.WriteByte(59); break; }
                if (marker == 33)
                {
                    int label = source[position++];
                    while (source[position] != 0) position += source[position] + 1;
                    position++;
                    if (label != 249) result.Write(source, start, position - start);
                }
                else if (marker == 44)
                {
                    byte packed = source[position + 8];
                    position += 9;
                    if ((packed & 128) != 0) position += 3 * (1 << ((packed & 7) + 1));
                    position++;
                    while (source[position] != 0) position += source[position] + 1;
                    position++;
                    byte[] timing = {33,249,4,4,7,0,0,0};
                    result.Write(timing, 0, timing.Length);
                    result.Write(source, start, position - start);
                    frames++;
                }
                else throw new Exception("Unexpected GIF block: " + marker);
            }
            File.WriteAllBytes(path, result.ToArray());
            return frames;
        }
    }
}
'@

$previewFolder = 'D:/project/ProjectCheon/Assets/Temp/CloudComboPreview~'
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$font = [System.Windows.Media.Typeface]::new('Segoe UI')
$background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(16,31,39))
$textBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(199,243,234))

$visual = New-Object System.Windows.Media.DrawingVisual
$drawing = $visual.RenderOpen()
$drawing.DrawRectangle($background, $null, [System.Windows.Rect]::new(0,0,1024,832))
$panels = @(
    @{ File='Test_CloudCombo_Attack2.png'; Label='ATTACK 2 / SWEEP'; Crop=[System.Windows.Int32Rect]::new(150,180,700,525) },
    @{ File='Test_CloudCombo_Attack3.png'; Label='ATTACK 3 / SWEEP + WIND'; Crop=[System.Windows.Int32Rect]::new(0,120,850,637) },
    @{ File='Test_CloudCombo_Wind1.png'; Label='FOLLOW-UP / SHORT GUST'; Crop=[System.Windows.Int32Rect]::new(210,240,640,480) },
    @{ File='Test_CloudCombo_Wind3.png'; Label='FOLLOW-UP / LONG GUST'; Crop=[System.Windows.Int32Rect]::new(210,150,720,540) }
)
for ($panelIndex=0; $panelIndex -lt $panels.Count; $panelIndex++) {
    $panel = $panels[$panelIndex]
    $offsetX = ($panelIndex % 2)*512
    $offsetY = [math]::Floor($panelIndex/2)*416
    $bitmap = [System.Windows.Media.Imaging.BitmapFrame]::Create([uri](Join-Path $previewFolder $panel.File))
    $cropped = [System.Windows.Media.Imaging.CroppedBitmap]::new($bitmap,$panel.Crop)
    $drawing.DrawImage($cropped,[System.Windows.Rect]::new($offsetX,$offsetY+32,512,384))
    $label = [System.Windows.Media.FormattedText]::new($panel.Label,$culture,[System.Windows.FlowDirection]::LeftToRight,$font,17,$textBrush,1)
    $drawing.DrawText($label,[System.Windows.Point]::new($offsetX+14,$offsetY+4))
}
$drawing.Close()
$render = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(1024,832,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$render.Render($visual)
$boardEncoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$boardEncoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($render))
$boardPath = Join-Path $previewFolder 'Test_CloudCombo_Overview.png'
$boardStream = [System.IO.File]::Open($boardPath,[System.IO.FileMode]::Create)
try { $boardEncoder.Save($boardStream) } finally { $boardStream.Dispose() }

$gifEncoder = New-Object System.Windows.Media.Imaging.GifBitmapEncoder
foreach ($attack in @(2,3)) {
    $frames = @(Get-ChildItem -LiteralPath $previewFolder -Filter ('Test_Attack'+$attack+'_Frame*.png') | Sort-Object Name | Select-Object -First 24)
    foreach ($frame in $frames) {
        $frameImage = [System.Windows.Media.Imaging.BitmapFrame]::Create([uri]$frame.FullName)
        $frameVisual = New-Object System.Windows.Media.DrawingVisual
        $frameDrawing = $frameVisual.RenderOpen()
        $frameDrawing.DrawRectangle($background,$null,[System.Windows.Rect]::new(0,0,768,608))
        $frameDrawing.DrawImage($frameImage,[System.Windows.Rect]::new(0,32,768,576))
        $labelText = if ($attack -eq 2) {'ATTACK 2 / CLOUD SWEEP'} else {'ATTACK 3 / CLOUD SWEEP + WIND FOLLOW-UP'}
        $frameLabel = [System.Windows.Media.FormattedText]::new($labelText,$culture,[System.Windows.FlowDirection]::LeftToRight,$font,17,$textBrush,1)
        $frameDrawing.DrawText($frameLabel,[System.Windows.Point]::new(14,4))
        $frameDrawing.Close()
        $frameRender = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(768,608,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
        $frameRender.Render($frameVisual)
        $gifEncoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($frameRender))
    }
}
$gifPath = Join-Path $previewFolder 'Test_CloudCombo_Playback.gif'
$gifStream = [System.IO.File]::Open($gifPath,[System.IO.FileMode]::Create)
try { $gifEncoder.Save($gifStream) } finally { $gifStream.Dispose() }
$gifFrames = [Test_CloudComboGifTiming]::Apply($gifPath)
$gifCheck = [System.Windows.Media.Imaging.GifBitmapDecoder]::new([uri]$gifPath,[System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
[pscustomobject]@{ Board=$boardPath; Gif=$gifPath; Frames=$gifFrames; Delay=$gifCheck.Frames[0].Metadata.GetQuery('/grctlext/Delay'); Width=$gifCheck.Frames[0].PixelWidth; Height=$gifCheck.Frames[0].PixelHeight }
