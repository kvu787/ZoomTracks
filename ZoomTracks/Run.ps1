Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

$logFolderPath = "C:\Users\k\Repository\Unity\ZoomTracks\ZoomTracks\MyLogOutput\$(Get-Date -Format "yyyy-MM-dd_HH-mm-ss")"
New-Item -ItemType "Directory" -Path $logFolderPath

$presentMonPath = "$env:UserProfile\Program\PresentMon-2.5.1-x64.exe"
if (Test-Path $presentMonPath) {
    $presentMonLogFilePath  = "$($logFolderPath)\PresentMon.csv"
    Start-Process `
        -FilePath $presentMonPath `
        -ArgumentList "--process_name `"ZoomTracks.exe`" --output_file `"$($presentMonLogFilePath)`"" `
        -Verb "RunAs"
}

$registryPath = "HKCU:\Software\K\ZoomTracks"
if (Test-Path $registryPath) {
  Remove-Item -Path $registryPath -Recurse -Force
}

$unityLogFilePath = "$($logFolderPath)\Unity.log"
$stutterLogFilePath = "$($logFolderPath)\Stutter.log"

# When specifying refresh rate, use exact frequency as reported by "Settings > System > Display > Advanced display".

# Monitor 1, dx12, 2560*1440, borderless fullscreen
$process = `
    Start-Process `
        -FilePath "C:\Users\k\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
        -ArgumentList "-monitor 1 -force-d3d12 -window-mode borderless -screen-width 2560 -screen-height 1440 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate -1 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
        -PassThru

# # Monitor 1, dx11, 2560*1440, borderless fullscreen
# $process = `
#     Start-Process `
#         -FilePath "C:\Users\k\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
#         -ArgumentList "-monitor 1 -force-d3d11 -window-mode borderless -force-d3d11-flip-model -screen-width 2560 -screen-height 1440 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate -1 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
#         -PassThru

$process.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High
Write-Host "Launched with PID=$($process.Id)"
Wait-Process -Id $process.Id
