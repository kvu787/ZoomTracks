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

# Main external screen, 180 hz, borderless exclusive fullscreen
$zoomTracksProcess = `
    Start-Process `
        -FilePath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
        -ArgumentList "-monitor 1 -force-d3d12 -window-mode borderless -screen-width 2560 -screen-height 1440 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate 179.84 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
        -PassThru

# # Main external screen, 120 hz, borderless exclusive fullscreen
# $zoomTracksProcess = `
#     Start-Process `
#         -FilePath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
#         -ArgumentList "-monitor 1 -force-d3d12 -window-mode borderless -screen-width 2560 -screen-height 1440 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate 120 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
#         -PassThru

# # Main external screen, 60 hz, borderless exclusive fullscreen
# $zoomTracksProcess = `
#     Start-Process `
#         -FilePath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
#         -ArgumentList "-monitor 1 -force-d3d12 -window-mode borderless -screen-width 2560 -screen-height 1440 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate 59.95 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
#         -PassThru

# # Main external screen, 180 hz, windowed
# $zoomTracksProcess = `
#     Start-Process `
#         -FilePath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
#         -ArgumentList "-monitor 1 -force-d3d12 -screen-fullscreen 0 -screen-width 2208 -screen-height 1242 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate 179.84 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
#         -PassThru

# # Laptop screen, windowed, 240 hz
# $zoomTracksProcess = `
#     Start-Process `
#         -FilePath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
#         -ArgumentList "-monitor 2 -force-d3d12 -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate 240 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
#         -PassThru

# # Laptop screen, windowed, 75 hz
# $zoomTracksProcess = `
#     Start-Process `
#         -FilePath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe" `
#         -ArgumentList "-monitor 2 -force-d3d12 -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile `"$($unityLogFilePath)`" -timestamps -refreshRate 75 -stutterLogFilePath `"$($stutterLogFilePath)`"" `
#         -PassThru

Wait-Process -Id $zoomTracksProcess.Id
