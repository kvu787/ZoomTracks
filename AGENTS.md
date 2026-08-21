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

# TrackBuilder tests

Testing architecture and commands are documented in
`Blender\TrackBuilder\Documentation\TEST.md`.

TrackBuilder test inputs in `Blender\TrackBuilder\TestInputs` are committed and
must contain only an `Input` collection. Regenerate and commit them only after a
deliberate test-fixture change:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py"
```

After any change that can affect TrackBuilder output, validation, fixtures, or
tests, run `Blender\TrackBuilder\TestTrackBuilder.py`. Each run writes inspectable
`.blend` results and an overall report to the gitignored
`Blender\TrackBuilder\TestArtifacts` directory. Never commit those artifacts.

# While reviewing the Unity C# code

Ignore this stuff:
* Commented out code
* graphics options are reset on track switch
* setup new scene tool can duplicate colliders and doesn't check if it's already been run
* fast cars can tunnel through obstacles
  * i know this can happen and it's fine

Ignore these currently unused features:
* TireGroundContactPoints
* MeshColliders on grass, gravel, track, etc
