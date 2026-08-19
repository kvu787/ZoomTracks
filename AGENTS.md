All of these repos are closely related and should be considered as one repo:

* `%userprofile%\Repository\SharedTools`
* `%userprofile%\Repository\Unity\ZoomTracks\ZoomTracks`
* `%userprofile%\Repository\Godot\VsyncStutterTest`

# Instructions

Never make any source code edits unless explicitly instructed in the prompt.
If you are recommending any edits, put them in the reply to the prompt.

# Available tools

* This should be on Windows 11.
* These additional tools should be available:
  * Modern PowerShell
  * Python
  * Git
* A portable installation of Blender should be in `%UserProfile%\Program\blender*`

# Info

* The root of the Unity Engine game project is the ZoomTracks subfolder.
* Game logs are in `C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyLogOutput`.
* Assume that the standalone build of the unity game is up-to-date and in `C:\Users\kevin\Repository\Unity\ZoomTracks\ZoomTracks\MyBuildOutput`.

# TrackBuilder generated examples

After any change that can affect TrackBuilder output or validation, regenerate
the tracked `.blend` examples and include their changes:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderExamples.py"
```

Then run `Blender\TrackBuilder\TestTrackBuilder.py`. The test suite deliberately
fails when the tracked example set or any successful example's saved `Output`
does not match a fresh build from the current TrackBuilder.

# While reviewing

Ignore this stuff:
* Commented out code
* graphics options are reset on track switch
* setup new scene tool can duplicate colliders and doesn't check if it's already been run
* fast cars can tunnel through obstacles
  * i know this can happen and it's fine

Ignore these currently unused features:
* TireGroundContactPoints
* MeshColliders on grass, gravel, track, etc
