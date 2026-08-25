# NOTE

Anyone can play this game, but it is configured specifically for my setup and preferences.

So, to play this, you'll probably need to adjust several things.

# Screenshot

![<Images/Screenshot 2026-08-25 151148.png>](<Images/Screenshot 2026-08-25 151148.png>)

# Description

The following is a succinct and accurate description of the game. This is especially helpful to provide to AI tools, because they often assume additional features and complexity that are inaccurate.

Zoom Tracks is a purely 2D racing game with a 3/4 overhead visual presentation. All physics, collision, movement, steering, and gameplay calculations occur on a flat 2D plane. The environment is completely static and can be represented by a flattened 2D image. The vehicle is the only moving and rotating visual element and may be rendered as a 3D mesh purely for appearance. When the vehicle is rendered in 3D, it uses a stylized unlit shader. Alternatively, the game can be fully visually represented with 2D shapes on a 2D plane. There is no elevation, 3D physics, dynamic lighting, shadows, foreground occlusion, or other moving world elements.

# How to run smoothly

Refer to "Recommended setup for Unity and Godot games" in https://github.com/kvu787/SharedTools/blob/main/README.md.

# temp run config

Change a variety of things to achieve min input latency, max locked fps conformance, and max motion clarity
These are the committed changes:

* Pass in -1 for -refreshRate to use Time.deltaTime for the timestep
  * Passing in R for -refreshRate where R matches the "Max Frame Rate" setting in NVCP probably also works
* Disable VSync by setting "Project Settings > Quality > VSync Count" = "Don't Sync"
* Disable the ability to toggle VSync in-game
* Switch from dx12 to dx11 by moving dx11 to the first item in the list of "Project Settings > Player > Other Settings > Graphics API for Windows"
* Enable "Project Settings > Player > Resolution and Presentation > Standalone Player Options > Use DXGI flip model swapchain for D3D11"
* Disable graphics jobs by disabling "Project Settings > Player > Other Settings > Graphics Jobs"
* Disable multithreaded rendering by passing in the "-force-gfx-direct" command line argument to ZoomTracks.exe
* Set SystemInfo.renderingThreadingMode to RenderingThreadingMode.Direct, which is automatically set after disabling graphics jobs and disabling multithreaded rendering

This was performance tested with the below configuration.

Note that the NVCP config is ***not*** automatically set or verified by the game. It must be manually configured by the player ***before*** launching ZoomTracks.exe.

* Borderless fullscreen windowed at native resolution
* Monitor with Nvidia G-Sync and Nvidia Pulsar
  * Example: XG27AQNGV
  * Set resolution to 2560*1440 and refresh rate to 360 Hz in Nvidia Control Panel (NVCP)
  * Enable Pulsar in the monitor OSD
  * If available, enable the monitor's built-in fps counter via the monitor OSD
    * Confirm that this OSD fps counter changes from 360 to the "NVCP Max Frame Rate" while running ZoomTracks.exe
    * This ensures that G-SYNC is working as expected
* In NVCP, configure this:
  * Set "Display > Set up G-SYNC > Apply following changes." = "Enable G-SYNC, G-SYNC Compatible + Enable for full screen mode"
  * Enable "Top bar > Display > G-SYNC Indicator" to confirm G-SYNC is active while playing ZoomTracks.exe
  * Create a custom profile for ZoomTracks.exe in "3D Settings > Manage 3D settings > Program Settings" with the following overrides:
    * Low Latency Mode = Ultra
    * Max Frame Rate = 240 FPS
      * Anything from 120 to 315 FPS should work too
    * Monitor Technology = G-SYNC
    * Power Management Mode = Prefer Maximum Performance
    * Vertical sync = On

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
