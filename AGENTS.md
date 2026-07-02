# Instructions

Never make any source code edits unless explicitly instructed in the prompt.
If you are recommending any edits, put them in the reply to the prompt.

# Available tools

* This should be on Windows 11.
* These additional tools should be available:
  * Modern PowerShell
  * Python
  * Git
* A portable installation of Blender should be in `$env:USERPROFILE\Program\blender-4.5.11-windows-x64`

# Info

The root of the Unity Engine game project is the ZoomTracks subfolder.

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
