Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

$logFolderPath = "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyLogOutput\$(Get-Date -Format "yyyy-MM-dd_HH-mm-ss")"
New-Item -ItemType "Directory" -Path $logFolderPath

$presentMonLogFilePath  = "$($logFolderPath)\PresentMon.csv"
Start-Process `
  -FilePath "C:\Users\kevin\Program\PresentMon-2.5.1-x64.exe" `
  -ArgumentList "--process_name `"ZoomTracks.exe`" --output_file `"$($presentMonLogFilePath)`" --terminate_on_proc_exit" `
  -Verb "RunAs"

$registryPath = "HKCU:\Software\K\ZoomTracks"
if (Test-Path $registryPath) {
  Remove-Item -Path $registryPath -Recurse -Force
}

Write-Host "HERE *****************************"

$unityLogFilePath = "$($logFolderPath)\Unity.log"
$stutterLogFilePath = "$($logFolderPath)\Stutter.log"

$zoomTracksProcess = `
    Start-Process `
        -FilePath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
        -ArgumentList "-force-d3d12 -window-mode borderless -screen-width 2560 -screen-height 1440 -logFile `"$($unityLogFilePath)`" -log-memory-performance-stats -timestamps -stutterLogFilePath `"$($stutterLogFilePath)`"" `
        -PassThru

Wait-Process -Id $zoomTracksProcess.Id
