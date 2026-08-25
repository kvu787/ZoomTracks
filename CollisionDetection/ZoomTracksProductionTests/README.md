# ZoomTracks production collision validation

This validation exercises the production Blender collider exporter against the
real Track001 source file and its committed collider JSON. Run it from the
repository root with Blender 4.5.12:

```powershell
& "$env:USERPROFILE\Program\blender-4.5.12-windows-x64\blender.exe" `
    --background `
    --factory-startup `
    "Blender\Tracks2\Track001.blend" `
    --python-exit-code 1 `
    --python "CollisionDetection\ZoomTracksProductionTests\ValidateTrack001ColliderExport.py"
```

The script invokes the complete `Blender/Scripts/ExportToZoomTracks.py` entry
point, writes the generated FBX and JSON to the gitignored `TestArtifacts`
directory, and compares the JSON semantically with
`ZoomTracks/Assets/StreamingAssets/Track001_ColliderData.json`. It also checks
the expected Track001 outline topology and the vehicle footprint and scale
assumptions used by production collision detection.

The collider JSON uses Blender world-space X/Y coordinates. The FBX asset's
axis metadata and Unity's Bake Axis Conversion setting map those coordinates to
Unity's ground plane as `(Unity X, Unity Z) = (-Blender X, -Blender Y)`.

## Production C# correctness and performance harness

The standalone .NET harness compiles the same pure-C# collider-data and
`TrackCollisionDetector` source files that Unity compiles. It loads the actual
committed Track001 collider JSON, checks its production grid invariants, compares
the optimized index with the production linear oracle across deterministic
random and every-edge-neighborhood workloads, verifies exact contact semantics
and arbitrary outline counts, exercises sparse-grid and oversized-edge BVH
fallbacks, checks steady-state allocations, and reports a comparative benchmark.

Run it from the repository root with .NET 10:

```powershell
dotnet run `
    --project "CollisionDetection\ZoomTracksProductionTests\ZoomTracksProductionTests.csproj" `
    --configuration Release
```

The benchmark reports relative optimized and linear timings but deliberately
does not impose an environment-dependent absolute timing threshold.

## Unity asset and integration validation

The Editor validator imports the real Track001 FBX, derives the car footprint
from its mesh bounds, deserializes the production collider JSON, and checks the
coordinate transform and detector layout/contact behavior. With the project not
already open in another Unity Editor, run:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe" `
    -batchmode `
    -nographics `
    -quit `
    -projectPath "ZoomTracks" `
    -executeMethod ZoomTracks.CollisionDetectionTrack001Validation.Execute `
    -logFile -
```

The same validation is available in the Editor at
`Tools > Collision Detection > Validate Track001`.
