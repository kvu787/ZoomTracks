These instructions assume you are using Blender 4.5.x LTS.

# Initial setup

* Delete all default objects
* Move Outliner and Properties sidebar from the right side to the left side
* Remove the Timeline editor area at the bottom
* Expand the N-sidebar

# Collections

* There should be only 1 item at the root: a collection called "Collection"
* Then, add the following collections:

```python
AllowedCollectionNames = [
    "Barriers",
    "BigCones",
    "Camera",
    "Checkpoints",
    "Cones",
    "Templates",
    "Track",
    "Uncategorized",
    "Vehicles",
]
```

# Outliner

In "Outliner > Filter > Restriction Toggles", enable only these:
* Checkbox
* Arrow
* Eye
* Monitor

# Viewport

* 3D Viewport > Top-right corner > Gizmos > Camera > Lens = Unchecked
* 3D Viewport > N-sidebar > View > View >
  * Clip start = 1 m
  * Clip end = 10000 m
* Properties > Render > Sampling > Shadows = False
* Properties > Render > Color Management > View Transform = Standard
* Properties > Render > Sampling > Viewport > Temporal Reprojection = Unchecked
* Properties > Render > Sampling > Viewport > Samples = 1

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
* Viewport Display > Show > Limit = True
* Distance from pivot to camera (Location.Z) = 500 m

# Materials

All materials should be Principled BSDF with default parameters.

Exception:
If you want a simple solid color with just shading and no specular, use these parameters:
1. Base color > Surface > Roughness = 1.0
2. Base color > Surface > Specular > IOR Level = 0.0

# Usage instructions

* Use "Viewport Shading = Material Preview" to see colors.
  * Do not use "Viewport Shading = Rendered".
* Do not add lighting objects.
  * Lighting is provided by the "Material Preview" environment.
