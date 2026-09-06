# Blender version

These instructions assume you are using Blender 4.5.x LTS.

# Initial setup

* Remove the Timeline editor area at the bottom
* Move Outliner and Properties sidebar from the right side to the left side
* Expand the N-sidebar
* Delete all default objects

# Collections

* There should be only 1 item at the root: a collection called "Collection"
* Then, add the following collections:

```
Camera/
Checkpoints/
Decorations/
  BigCones/
  Decorations/
Templates/
Vehicles/
TrackBuilder/
  Input/
    Outlines/
```

# Outliner

In "Outliner > Filter > Restriction Toggles", enable only these:

* Checkbox
* Arrow
* Eye

Outliner > Filter > Filter > Object Contents = Unchecked

# Viewport

* 3D Viewport >
  * Top-right corner > Gizmos > Camera > Lens = Unchecked
  * Viewport Overlays > Statistics = Checked
  * N-sidebar > View > View > Clip start = 1 m
  * N-sidebar > View > View > Clip end = 10000 m
* Properties > Render >
  * Sampling >
    * Viewport >
      * Samples = 1
      * Temporal Reprojection = Unchecked
    * Shadows = Unchecked
  * Color Management > View Transform = Standard

# Camera objects

Camera pivot:
* Name: "CameraPivot"
* Plain axes
  * Display As = Arrows
  * Size = 10 m
* Pitch = 45 deg (Rotation.X)
* Yaw   = 45 deg (Rotation.Z)

Camera:
* Name: "Camera"
* Type = Orthographic
* Orthographic Scale = 300
* Clip Start = 1 m
* Clip End = 1000 m
* Properties > Data > Viewport Display > Show > Limit = Checked
* Distance from pivot to camera (Location.Z) = 500 m

# Materials

All materials should be Principled BSDF with default parameters.

Exception:
If you want a simple solid color with just shading and no specular, use these parameters:
1. Base color > Surface > Roughness = 1.0
2. Base color > Surface > Specular > IOR Level = 0.0

# Track objects

* In the Templates collection, add the BarrierSegment and CheckeredLineSegment objects
* In the Track collection, add a simple oval track that uses each object:
  * Rectangular grass area
  *

# Geometry nodes

After adding the template track, you should have these custom geometry nodes modifiers:
* GenerateBarrier
* GenerateCheckeredLine

All custom geometry nodes modifiers should have "Fake User" enabled.

# Usage instructions

* Use "Viewport Shading = Material Preview" to see colors.
  * Do not use "Viewport Shading = Rendered".
* Do not add lighting objects.
  * Lighting is provided by the "Material Preview" environment.
