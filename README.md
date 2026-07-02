![<Images/README pic.png>](<Images/README pic.png>)

```
.\ZoomTracks.exe -window-mode "exclusive"
.\ZoomTracks.exe -force-vulkan
.\ZoomTracks.exe -force-glcore

opengl, exclusive fullscreen, native render resolution:
.\ZoomTracks.exe -force-glcore -window-mode "exclusive" -screen-width "2560" -screen-height "1440"

opengl, borderless fullscreen, native render resolution:
.\ZoomTracks.exe -force-glcore -window-mode "borderless" -screen-width "2560" -screen-height "1440"

opengl, windowed:
.\ZoomTracks.exe -force-glcore -screen-fullscreen "0" -screen-width "2208" -screen-height "1242"

vulkan, windowed:
.\ZoomTracks.exe -force-vulkan -screen-fullscreen "0" -screen-width "2208" -screen-height "1242"

dx12, exclusive fullscreen, native render resolution:
.\ZoomTracks.exe -force-d3d12 -window-mode "exclusive" -screen-width "2560" -screen-height "1440"

dx12, borderless fullscreen, native render resolution:
.\ZoomTracks.exe -force-d3d12 -window-mode "borderless" -screen-width "2560" -screen-height "1440"

dx12, windowed:
.\ZoomTracks.exe -force-d3d12 -screen-fullscreen "0" -screen-width "2208" -screen-height "1242"

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
```
