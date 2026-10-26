$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;
public static class Test_GifTiming
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
                    byte[] timing = {33,249,4,4,10,0,0,0};
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

$previewFolder = 'D:/project/ProjectCheon/Assets/Temp/CloudWindPreview~'
$gifPath = Join-Path $previewFolder 'Test_CloudWind_Attack1.gif'
$existingGif = [System.Windows.Media.Imaging.GifBitmapDecoder]::new([uri]$gifPath, [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
if ($existingGif.Frames[0].Metadata.GetQuery('/grctlext/Delay') -eq 10) {
    $gifFrames = $existingGif.Frames.Count
} else {
    $gifFrames = [Test_GifTiming]::Apply($gifPath)
}
$visual = New-Object System.Windows.Media.DrawingVisual
$drawing = $visual.RenderOpen()
$drawing.DrawRectangle([System.Windows.Media.Brushes]::Black, $null, [System.Windows.Rect]::new(0,0,1024,600))
$culture = [System.Globalization.CultureInfo]::GetCultureInfo('ko-KR')
$font = [System.Windows.Media.Typeface]::new('Malgun Gothic')
$panelIndex = 0
foreach ($panel in @(@{ File='Test_CloudWind_Before.png'; Label='BEFORE / ATTACK 1' }, @{ File='Test_CloudWind_After.png'; Label='AFTER / CLOUD WIND' })) {
    $bitmap = [System.Windows.Media.Imaging.BitmapFrame]::Create([uri](Join-Path $previewFolder $panel.File))
    $cropped = [System.Windows.Media.Imaging.CroppedBitmap]::new($bitmap, [System.Windows.Int32Rect]::new(240,32,640,704))
    $drawing.DrawImage($cropped, [System.Windows.Rect]::new($panelIndex*512,37,512,563))
    $label = [System.Windows.Media.FormattedText]::new($panel.Label,$culture,[System.Windows.FlowDirection]::LeftToRight,$font,19,[System.Windows.Media.Brushes]::White,1)
    $drawing.DrawText($label,[System.Windows.Point]::new($panelIndex*512+18,5))
    $panelIndex++
}
$drawing.Close()
$render = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(1024,600,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$render.Render($visual)
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($render))
$comparisonPath = Join-Path $previewFolder 'Test_CloudWind_Comparison.png'
$stream = [System.IO.File]::Open($comparisonPath,[System.IO.FileMode]::Create)
try { $encoder.Save($stream) } finally { $stream.Dispose() }
[pscustomobject]@{ Gif=$gifPath; Frames=$gifFrames; Comparison=$comparisonPath }
