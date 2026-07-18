Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

$logFolderPath = "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyLogOutput\$(Get-Date -Format "yyyy-MM-dd_HH-mm-ss")"
New-Item -ItemType "Directory" -Path $logFolderPath

# # PresentMon
# $presentMonLogFilePath  = "$($logFolderPath)\PresentMon.csv"
# Start-Process `
#   -FilePath "C:\Users\kevin\Program\PresentMon-2.5.1-x64.exe" `
#   -ArgumentList "--process_name `"ZoomTracks.exe`" --output_file `"$($presentMonLogFilePath)`"" `
#   -Verb "RunAs"

$registryPath = "HKCU:\Software\K\ZoomTracks"
if (Test-Path $registryPath) {
  Remove-Item -Path $registryPath -Recurse -Force
}

$unityLogFilePath = "$($logFolderPath)\Unity.log"
$stutterLogFilePath = "$($logFolderPath)\Stutter.log"

# Use exact frequency as reported by "Settings > System > Display > Advanced display"
# $refreshRate = 179.84

# Main monitor, 4K, 240 hz, borderless fullscreen
$zoomTracksProcess = `
    Start-Process `
        -FilePath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
        -ArgumentList "-monitor 1 -force-d3d12 -window-mode borderless -screen-width 3840 -screen-height 2160 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate 240.08 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
        -PassThru

# # Main monitor, 4K, 240 hz, exclusive fullscreen
# $zoomTracksProcess = `
#     Start-Process `
#         -FilePath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
#         -ArgumentList "-monitor 1 -force-d3d12 -window-mode exclusive -screen-width 3840 -screen-height 2160 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate 240.08 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
#         -PassThru

Wait-Process -Id $zoomTracksProcess.Id
