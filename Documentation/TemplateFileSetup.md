* Use Blender 4.5.x LTS
* Delete all default objects
* Move Outliner and Properties sidebar from the right side to the left side

* Use "Viewport Shading = Material Preview" to see colors
  * Do not use "Viewport Shading = Rendered"

# Materials

All materials should be Principled BSDF with default parameters.

Exception:
If you want a simple solid color with just shading and no specular, use these parameters:
1. Base color > Surface > Roughness = 1.0
2. Base color > Surface > Specular > IOR Level = 0.0

# Objects

Do not add lighting objects. Lighting is provided by the "Material Preview" environment.

Camera pivot
  * Plain axes
    * Display As = Arrows
    * Size = 10 m
  * Pitch = 45 deg
  * Distance from pivot to camera (zoom) = 50 m

Camera
  * Type = Orthographic
  * Orthographic Scale = 100
  * Clip Start = 1 m
  * Clip End = 150 m
  * Viewport Display > Show > Limit = True

# Viewport

* Gizmos: Disable "Camera > Lens"
* Clip start = 1 m
* Clip end = 10000 m
* Render > Sampling > Shadows = False
* Scene > Color Management > View Transform = Standard
* Disable temporal reprojection
* Viewport samples = 8

# Outliner

In "Outliner > Filter > Restriction Toggles", enable only these:
* Checkbox
* Arrow
* Eye
* Monitor
