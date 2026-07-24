# NOTE

Anyone can play this game, but it is configured specifically for my setup and preferences.

So, to play this, you'll probably need to adjust several things.

# Screenshot

![<Images/README pic.png>](<Images/README pic.png>)

# Known good run config (July 21 2026)

* print out SystemInfo.renderingThreadingMode and ensure it is set to RenderingThreadingMode.Direct
  * SystemInfo.renderingThreadingMode cannot be directly set.
  * To get it to return RenderingThreadingMode.Direct, you need to do these two things:
    * Disable graphics jobs
    * Disable multithreaded rendering
* add -force-gfx-direct to command line arg
* disable v-sync by default, both in project settings (quality settings) and c# code
* disable graphics jobs
* project settings:
  * use flip model swapchain for dx11
  * disable graphics jobs
  * use dx11 by moving it to the first item in graphics apis list


& "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -projectPath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks" -force-d3d11
& "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -projectPath "C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks"

NVCP warning
* i speculate that there could be things that cause nvcp config to get into a "bad state"
* example: switching between hybrid and dgpu mode
* the reason i suspect this is that the laptop's built-in display is visible in NVCP in dgpu mode, but invisible in NVCP in hybrid mode.
* so the question is: when switching between hybrid and dgpu mode, is the NVCP config properly "migrated"?
* currently, the only way to know for sure is to do a clean driver reinstall after toggling between hybrid/dgpu mode, which is cumbersome
* you can also to "restore defaults" for each section in NVCP, but that seems less certain than a clean driver reinstall

general setup
* use dgpu-only mode, not hybrid mode
  * i think that hybrid mode works fine, but i've done more testing with dgpu mode
* monitor = Asus ROG Strix Pulsar XG27AQNGV
* install nvidia 596.49 and select "clean installation"
  * this ensures that all nvcp settings are reset to default
  * unfortunately, it seems like the only way to guarantee that all nvcp settings are reset to default is to do a full driver installation and select "clean install"
* enable gsync and pulsar
* set monitor refresh rate to max (360hz) in nvcp
* disable vsync and any kind of frame rate limiter both in the game code and in external tools
* nvcp:
  * in global settings, set "Preferred graphics processor" to "High-performance NVIDIA processor"
  * in global settings, set power management mode to maximum in nvcp
  * create exe-specific profiles and set "max refresh rate" as desired

nvidia control panel profile
* set up g-sync = enabled for fullscreen
* max frame rate = min of 120, max of 315
* low latency mode = ultra
* monitor technology = g-sync
* power management mode = prefer maximum performance
* vertical sync = on

do not change any of the windows graphics settings from default:
* hags should stay on
* optimizations for windowed games should stay on
* variable refresh rate should stay on

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
    * 7. Can't change the brightness of the laptop screen via "Settings > System > Display" or fn+F5/F6
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
    * DON'T install the Nvidia App.
    * Some configuration operations in the Nvidia App are buggy. Example: per-program G-Sync settings
    * Only use the Nvidia Control Panel.
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
