Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

$logFolderPath = "C:\Users\k\Repository\Unity\ZoomTracks\ZoomTracks\MyLogOutput\$(Get-Date -Format "yyyy-MM-dd_HH-mm-ss")"
New-Item -ItemType "Directory" -Path $logFolderPath

# PresentMon
$presentMonLogFilePath  = "$($logFolderPath)\PresentMon.csv"
Start-Process `
  -FilePath "C:\Users\k\Program\PresentMon-2.5.1-x64.exe" `
  -ArgumentList "--process_name `"ZoomTracks.exe`" --output_file `"$($presentMonLogFilePath)`"" `
  -Verb "RunAs"

$registryPath = "HKCU:\Software\K\ZoomTracks"
if (Test-Path $registryPath) {
  Remove-Item -Path $registryPath -Recurse -Force
}

$unityLogFilePath = "$($logFolderPath)\Unity.log"
$stutterLogFilePath = "$($logFolderPath)\Stutter.log"

# When specifying refresh rate, use exact frequency as reported by "Settings > System > Display > Advanced display".

# Monitor 1, dx11, 3840*2400, borderless fullscreen
$process = `
    Start-Process `
        -FilePath "C:\Users\k\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
        -ArgumentList "-monitor 1 -force-d3d11 -force-gfx-direct  -window-mode borderless -force-d3d11-flip-model -screen-width 3840 -screen-height 2400 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate -1 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
        -PassThru

$process.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High
Write-Host "Launched with PID=$($process.Id)"
Wait-Process -Id $process.Id
