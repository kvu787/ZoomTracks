# Screenshot

![<Images/README pic.png>](<Images/README pic.png>)

# Run configuration

```powershell
.\ZoomTracks.exe -window-mode "exclusive"
.\ZoomTracks.exe -force-vulkan
.\ZoomTracks.exe -force-glcore

dx12, exclusive fullscreen, native render resolution:
.\ZoomTracks.exe -force-d3d12 -window-mode "exclusive" -screen-width "2560" -screen-height "1440"
dx12, borderless fullscreen, native render resolution:
.\ZoomTracks.exe -force-d3d12 -window-mode "borderless" -screen-width "2560" -screen-height "1440"
dx12, windowed:
.\ZoomTracks.exe -force-d3d12 -screen-fullscreen "0" -screen-width "2208" -screen-height "1242"

vulkan, borderless fullscreen, native render resolution:
.\ZoomTracks.exe -force-vulkan -window-mode "borderless" -screen-width "2560" -screen-height "1440"
vulkan, windowed:
.\ZoomTracks.exe -force-vulkan -screen-fullscreen "0" -screen-width "2208" -screen-height "1242"

dx11, exclusive fullscreen, native render resolution:
.\ZoomTracks.exe -force-d3d11 -window-mode "exclusive" -screen-width "2560" -screen-height "1440"
dx11, borderless fullscreen, flip, native render resolution:
.\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-flip-model -screen-width "2560" -screen-height "1440"
dx11, borderless fullscreen, flip, non-native render resolution:
.\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-flip-model -screen-width "640" -screen-height "480"
dx11, borderless fullscreen, blit, native render resolution:
.\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-bitblt-model -screen-width "2560" -screen-height "1440"
dx11, borderless fullscreen, blit, non-native render resolution:
.\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-bitblt-model -screen-width "640" -screen-height "480"
dx11, borderless non-fullscreen:
.\ZoomTracks.exe -force-d3d11 -popupwindow -screen-fullscreen "0" -screen-width "640" -screen-height "480"
dx11, windowed, flip:
.\ZoomTracks.exe -force-d3d11 -screen-fullscreen "0" -screen-width "2208" -screen-height "1242" -force-d3d11-flip-model
dx11, windowed, blit:
.\ZoomTracks.exe -force-d3d11 -screen-fullscreen "0" -screen-width "2208" -screen-height "1242" -force-d3d11-bitblt-model

opengl, exclusive fullscreen, native render resolution:
.\ZoomTracks.exe -force-glcore -window-mode "exclusive" -screen-width "2560" -screen-height "1440"
opengl, borderless fullscreen, native render resolution:
.\ZoomTracks.exe -force-glcore -window-mode "borderless" -screen-width "2560" -screen-height "1440"
opengl, windowed:
.\ZoomTracks.exe -force-glcore -screen-fullscreen "0" -screen-width "2208" -screen-height "1242"
```

```powershell
I am testing these configurations:

dx12, exclusive fullscreen, native render resolution:
.\ZoomTracks.exe -force-d3d12 -window-mode "exclusive" -screen-width "2560" -screen-height "1440"

dx12, borderless fullscreen, native render resolution:
.\ZoomTracks.exe -force-d3d12 -window-mode "borderless" -screen-width "2560" -screen-height "1440"

dx11, exclusive fullscreen, native render resolution:
.\ZoomTracks.exe -force-d3d11 -window-mode "exclusive" -screen-width "2560" -screen-height "1440"

dx11, borderless fullscreen, flip, native render resolution:
.\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-flip-model -screen-width "2560" -screen-height "1440"

dx11, borderless fullscreen, blit, native render resolution:
.\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-bitblt-model -screen-width "2560" -screen-height "1440"
```

# Guide for reproducing stutters

Do this for all reproductions:
* use commit e91fdc8835ef591a30939cd2dcbb26f59c016ebb
* play the game on the primary monitor
* play non-fullscreen, maximized windowed, non-theater mode youtube livestream vod with the chat panel open at max quality on an edge browser window on the secondary monitory (example: https://www.youtube.com/watch?v=Tg_4D1dfP-o)

Reproduction for consistent and immediate stutter:
* primary: t27hv-20, 2560*1440, 59.95 hz or 74.78 hz
* secondary: dell u2717d, 2560*1440, 59.95 hz
* .\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-bitblt-model -screen-width "2560" -screen-height "1440"

Reproduction for stutter starting 7 to 10 minutes in:
* primary: t27hv-20, 2560*1440, 59.95 hz
* secondary: dell u2717d, 2560*1440, 59.95 hz
* .\ZoomTracks.exe -force-d3d12 -window-mode "exclusive" -screen-width "2560" -screen-height "1440"
* .\ZoomTracks.exe -force-d3d12 -window-mode "borderless" -screen-width "2560" -screen-height "1440"
* .\ZoomTracks.exe -force-d3d11 -window-mode "exclusive" -screen-width "2560" -screen-height "1440"
* .\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-flip-model -screen-width "2560" -screen-height "1440"

When the secondary monitor is disconnected (which leaves the primary monitor as the only display),
I observe no stuttering. Also, I observe that the game looks much "smoother" throughout the whole
game runtime, including at the very start of the game. This overall smoothness isn't a lack of
stutters. It is just a general visual "smoothness" that you can tell when you play with
primary+secondary monitor versus just with the primary monitor. So, this must another issue with
using two monitors in addition to the stuttering issue I was originally investigating.

^^^
this observation is wrong. i did some more tests and it seems like the increased general smoothness
i observed was just me running at 75 hz monitor refresh instead of 60 hz like i thought i was


Remove-Item -Path "HKCU:\Software\K\ZoomTracks" -Recurse


```powershell
# in admin powershell window
# $label = "dual monitor"
$label = "dual monitor dx12 borderless"
$date = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$out  = "C:\Users\kevin\Repository\Unity\ZoomTracks\PresentMonCaptures\${date} ${label}.csv"
C:\Users\kevin\Repository\Unity\ZoomTracks\PresentMon-2.5.1-x64.exe --process_name ZoomTracks.exe --output_file $out --timed 600 --terminate_after_timed

# in standard powershell window
Remove-Item -Path "HKCU:\Software\K\ZoomTracks" -Recurse; C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput\ZoomTracks.exe -force-d3d12 -window-mode "borderless" -screen-width "2560" -screen-height "1440"
```
