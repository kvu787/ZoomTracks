# NOTE

Anyone can play this game, but it is configured specifically for my setup and preferences.

So, to play this, you'll probably need to adjust several things.

# Screenshot

![<Images/README pic.png>](<Images/README pic.png>)

# Known good run config

The following configuration is the result a lot of testing to eliminate stutters and to get the colors right.

* Computer:
  * Model = Lenovo Legion 9 18IAX10 (aka Lenovo Legion 9i)
  * CPU = Intel 275HX
  * GPU = Nvidia 5090 laptop
  * Laptop is closed (so its built-in display should be inactive)
  * Use the default setting of "Legion Space > GPU Working Mode = Hybrid Mode".
  * Use "Legion Space > GPU Working Mode > Hybrid Mode > Smart iGPU Mode = Off".
    * I've never tried setting this to "On" or "Auto".
    * It's supposed to intelligently turn off the dGPU to save power, but that is irrelevant for my current desktop-only usage because the external monitors require the dGPU.
  * Don't use "Legion Space > GPU Working Mode = dGPU Mode".
    * I have observed several issues with dGPU mode.
    * 1. Using external monitors while having the laptop lid closed leads to odd display issues, such as stuttering after waking from sleep when the external monitors have significantly different refresh rates.
    * 2. Connecting certain monitors using Thunderbolt 5 USB-C (laptop side) to DisplayPort (monitor side) leads to the NV-Failsafe issue and the inability to display anything other than 640*480 resolution.
      * This was observed with the PA278CGRV, but not with the PA278CV.
      * Workarounds included connecting the PA278CGRV via HDMI to HDMI or via USB-C to USB-C.
    * 3. The laptop runs abnormally hot even when idling.
    * 4. I didn't observe the supposed benefits of dGPU-only mode, which are improved performance and theoretically simpler operation.
    * 5. dGPU mode fails to run the built-in screen, a 4k@240Hz external monitor, and a 1440p@60Hz external monitor simultaneously.
    * 6. Having multiple external monitors at different refresh rates connected leads to occasional "blinking" on some monitors.
* Input:
  * Razer Wolverine V3 Pro 8K PC
  * Connected via wireless dongle
  * (Wired should work fine too)
* Display:
  * Model = Asus PA278CV
  * Resolution = 2560 x 1440
  * Refresh rate = 59.95 Hz
  * Response time = 5 ms
  * Reset to factory settings with "OSD > System Setup > All Reset"
    * Then, change these settings:
    * OSD > ProArt Preset = Rec. 709 Mode
    * OSD > ProArt Palette > Brightness = 65
    * OSD > ProArt Palette > Gamma = 2.2
    * OSD > Image > Trace Free = 0
  * This display is connected via HDMI to the laptop.
  * This is the only active display.
* Software
  * Nvidia Studio Driver 596.36 or GeForce Game Ready Driver 596.49
  * Windows 11 Pro, Version 25H2 (OS Build 26200.8655)
  * To avoid stutters in fullscreen (exclusive or borderless modes), disable "Hardware-accelerated GPU scheduling"
  * Night light enabled with strength = 50

Run the game by double-clicking `Run-ZoomTracks.cmd` in File Explorer.

# Run configurations

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

# Clear registry keys

```powershell
Remove-Item -Path "HKCU:\Software\K\ZoomTracks" -Recurse
```

# Guide for reproducing stutters

With a single monitor, only dx11 with bit blit has stutters.
With dual monitors, all DirectX configurations have stutters starting 7 to 10 minutes after game start.
I haven't thoroughly tested Vulkan yet.

Do this for all reproductions:
* use commit e91fdc8835ef591a30939cd2dcbb26f59c016ebb
* play the game on the primary monitor
* play non-fullscreen, maximized windowed, non-theater mode youtube livestream vod with the chat
  panel open at max quality on an edge browser window on the secondary monitory
  * Example: https://www.youtube.com/watch?v=Tg_4D1dfP-o

Reproduction for consistent and immediate stutter, for both single and dual monitor setups:
* dx11, borderless fullscreen, blit, native render resolution:
* .\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-bitblt-model -screen-width "2560" -screen-height "1440"

Reproduction for stutter starting 7 to 10 minutes in, dual monitor only:
* primary: lenovo t27hv-20, 2560*1440, 59.95 hz
* secondary: dell u2717d, 2560*1440, 59.95 hz
* .\ZoomTracks.exe -force-d3d12 -window-mode "exclusive" -screen-width "2560" -screen-height "1440"
* .\ZoomTracks.exe -force-d3d12 -window-mode "borderless" -screen-width "2560" -screen-height "1440"
* .\ZoomTracks.exe -force-d3d11 -window-mode "exclusive" -screen-width "2560" -screen-height "1440"
* .\ZoomTracks.exe -force-d3d11 -window-mode "borderless" -force-d3d11-flip-model -screen-width "2560" -screen-height "1440"
