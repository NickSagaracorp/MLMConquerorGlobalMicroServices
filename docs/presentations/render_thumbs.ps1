param(
  [string]$Pptx = "C:\Users\sagar\source\repos\ClaudeRepository\docs\presentations\2026-05-24-mlm-development-status.pptx",
  [string]$OutDir = "C:\Users\sagar\source\repos\ClaudeRepository\docs\presentations\thumbs"
)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$ppt = New-Object -ComObject PowerPoint.Application
$ppt.Visible = [Microsoft.Office.Core.MsoTriState]::msoTrue
$pres = $ppt.Presentations.Open($Pptx, $true, $true, $false)
$i = 1
foreach ($slide in $pres.Slides) {
  $name = "slide-{0:D2}.png" -f $i
  $path = Join-Path $OutDir $name
  $slide.Export($path, "PNG", 1600, 900)
  Write-Host "Wrote $path"
  $i++
}
$pres.Close()
$ppt.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($ppt) | Out-Null
