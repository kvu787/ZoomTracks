# Base template

## External tools

You may use the tools in `%UserProfile%\Program`.
You may refer to local copies of source repos in `%UserProfile%\Repository\External`.

## Godot

If you create a Godot project, include a "Run.cmd" file that builds and launches the standalone exe of the Godot project by double-clicking the Run.cmd from File Explorer.

## Git

When implementing stuff, avoid difficult-to-review "mega-commits".
Split large work into multiple commits to make it easier to review.
Separate commits that record conversations from other commits.

## Mathematical notation in Markdown

Any mathematical notation in Markdown files (LaTeX, KaTeX, MathJax, etc) must display properly in VSCode's Markdown previewer, GitHub.com's Markdown displayer, and the markdown viewer in the Windows 11 ChatGPT app.

# Base template additions

## Compatibility

Do not attempt to maintain any sort of application compatibility between different commits of the repo. This creates unwanted complexity.

# Repository-specific

## Info

* The root of the Unity Engine game project is the ZoomTracks subfolder.
* Game logs are in `ZoomTracks\MyLogOutput`.

## TrackBuilder tests

Testing architecture and commands are documented in
`Blender\TrackBuilder\Documentation\TEST.md`.

TrackBuilder test inputs in `Blender\TrackBuilder\TestInputs` are committed and
must contain `TrackBuilder/Input/Outlines` without a generated
`TrackBuilder/Output`. Regenerate and commit them only after a deliberate
test-fixture change:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" --background --factory-startup --python-exit-code 1 --python "Blender\TrackBuilder\GenerateTrackBuilderSamples.py"
```

After any change that can affect TrackBuilder output, validation, fixtures, or
tests, run `Blender\TrackBuilder\TestTrackBuilder.py`. Each run writes inspectable
`.blend` results and an overall report to the gitignored
`Blender\TrackBuilder\TestArtifacts` directory. Never commit those artifacts.

## While reviewing the Unity C# code

Ignore this stuff:
* Commented out code
* graphics options are reset on track switch
* setup new scene tool can duplicate colliders and doesn't check if it's already been run
* fast cars can tunnel through obstacles
  * i know this can happen and it's fine

Ignore these currently unused features:
* TireGroundContactPoints
* MeshColliders on grass, gravel, track, etc
