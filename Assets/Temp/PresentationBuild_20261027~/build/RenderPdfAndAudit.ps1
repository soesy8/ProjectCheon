$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
Add-Type -AssemblyName System.IO.Compression.FileSystem
[Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime] | Out-Null
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Storage.Streams.InMemoryRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime] | Out-Null
[Windows.Data.Pdf.PdfPageRenderOptions,Windows.Data.Pdf,ContentType=WindowsRuntime] | Out-Null
$asyncMethods=[System.WindowsRuntimeSystemExtensions].GetMethods()
$opMethod=$asyncMethods | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and $_.GetGenericArguments().Count -eq 1 -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
$actionMethod=$asyncMethods | Where-Object { $_.Name -eq 'AsTask' -and !$_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' } | Select-Object -First 1
function AwaitResult($operation,[Type]$resultType){
    $task=$opMethod.MakeGenericMethod($resultType).Invoke($null,@($operation))
    $task.Wait()
    return $task.Result
}
function AwaitAction($operation){
    $task=$actionMethod.Invoke($null,@($operation))
    $task.Wait()
}
$root='D:\project\ProjectCheon'
$work=Join-Path $root 'Assets\Temp\PresentationBuild_20261027~'
$out=Join-Path $root 'Deliverables\Cheonjiin_20261027'
$pdf=Join-Path $out '천지인_기획서_슬라이드_20261027_v2.pdf'
$pptx=Join-Path $out '천지인_기획서_슬라이드_20261027_v2.pptx'
$pdfPreview=Join-Path $work 'pdf_previews'
New-Item -ItemType Directory -Path $pdfPreview -Force | Out-Null
$file=AwaitResult ([Windows.Storage.StorageFile]::GetFileFromPathAsync($pdf)) ([Windows.Storage.StorageFile])
$doc=AwaitResult ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])
if($doc.PageCount -ne 22){throw 'Wrong PDF page count'}
for($index=0;$index -lt $doc.PageCount;$index++){
    $page=$doc.GetPage($index)
    $memory=[Windows.Storage.Streams.InMemoryRandomAccessStream]::new()
    $options=[Windows.Data.Pdf.PdfPageRenderOptions]::new()
    $options.DestinationWidth=1440; $options.DestinationHeight=810
    AwaitAction ($page.RenderToStreamAsync($memory,$options))
    $stream=[System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($memory)
    $stream.Position=0
    $dest=[System.IO.File]::Create((Join-Path $pdfPreview ('page_{0:00}.png' -f ($index+1))))
    $stream.CopyTo($dest);$dest.Dispose();$stream.Dispose();$page.Dispose()
}
$zip=[System.IO.Compression.ZipFile]::OpenRead($pptx)
try{
    $slides=@($zip.Entries | Where-Object FullName -Match '^ppt/slides/slide\d+\.xml$')
    $notes=@($zip.Entries | Where-Object FullName -Match '^ppt/notesSlides/notesSlide\d+\.xml$')
    $tableCount=0;$textCount=0;$xmlCount=0
    foreach($entry in $zip.Entries){
        if($entry.FullName -match '\.(xml|rels)$'){
            $reader=[IO.StreamReader]::new($entry.Open());$xml=[xml]$reader.ReadToEnd();$reader.Dispose();$xmlCount++
            if($entry.FullName -match '^ppt/slides/slide\d+\.xml$'){
                $ns=[Xml.XmlNamespaceManager]::new($xml.NameTable)
                $ns.AddNamespace('a','http://schemas.openxmlformats.org/drawingml/2006/main')
                $tableCount+=$xml.SelectNodes('//a:tbl',$ns).Count
                $textCount+=$xml.SelectNodes('//a:t',$ns).Count
            }
        }
    }
    if($slides.Count -ne 22 -or $notes.Count -ne 22){throw 'PPTX slide/notes count mismatch'}
}finally{$zip.Dispose()}
$before=Get-Content (Join-Path $work 'build\source_hashes_before.json') -Raw -Encoding utf8 | ConvertFrom-Json
$changed=@()
foreach($record in $before){
    if(!(Test-Path -LiteralPath $record.Path) -or (Get-FileHash -LiteralPath $record.Path -Algorithm SHA256).Hash -ne $record.Hash){$changed+=$record.Path}
}
$report=@{slides=$slides.Count;pdfPages=$doc.PageCount;nativeTables=$tableCount;editableTextRuns=$textCount;validXmlParts=$xmlCount;notes=$notes.Count;sourceFilesChecked=$before.Count;sourceFilesChanged=$changed;fonts=@('Noto Serif KR','Noto Sans KR');pptx=$pptx;pdf=$pdf;authoring='Native PowerPoint automation; required artifact runtime unavailable';pdfRendering='Windows.Data.Pdf native renderer'}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $work 'build\final_validation.json') -Encoding utf8
Write-Output ($report | ConvertTo-Json -Depth 5)
if($changed.Count -gt 0){throw 'Source hashes changed'}
