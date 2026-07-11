param(
    [Parameter(Mandatory)]
    [string]$FilePath,

    [Parameter(Mandatory)]
    [int]$NumParts
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

if ($NumParts -lt 2) {
    throw "NumParts must be 2 or greater"
}

$lines = Get-Content -LiteralPath $FilePath
$file = Get-Item -LiteralPath $FilePath
$directory = $file.DirectoryName
$baseName = $file.BaseName
$extension = $file.Extension

$partSize = [math]::Floor($lines.Count / $NumParts)
$ranges = @()

for ($i = 0; $i -lt $NumParts; $i++) {
    $ranges += $i * $partSize
    if ($i -eq ($NumParts - 1)) {
        $ranges += $lines.Count - 1
    } else {
        $ranges += (($i + 1) * $partSize) - 1
    }
}

$partNumber = 1
for ($i = 0; $i -lt $ranges.Count; $i += 2) {
    $outputPath = Join-Path $directory "$($baseName) part $($partNumber)$($extension)"
    $lines[$ranges[$i]..$ranges[$i + 1]] | Set-Content -LiteralPath $outputPath
    $partNumber += 1
}
