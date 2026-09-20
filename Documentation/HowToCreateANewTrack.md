# How to create a new track

- Copy/paste TrackTemplate.blend to a new file
- In the Outliner, exclude these collections:
  * ColorBlocks
  * Templates
- You probably want to exclude Checkpoints/Defaults because checkpoint functionality isn't implemented yet.
- Don't modify anything in Decorations/Defaults other than moving their positions
  - Instead duplicate them into your track
  - Exclude Decorations/Defaults when you're done
- Copy/paste the latest TrackBuilder.py into a script pane
- After finishing your input track outlines, run TrackBuilder
- Adjust vehicle road, placeholder car, and checkered line objects as desired
- Add decorative objects as desired
- Before exporting, do this:
  - Exclude these collections:
    - TrackBuilder/Input
    - Checkpoints/Defaults
    - Decorations/Defaults
  - Run ValidateTrackScene.py and fix any issues
- To export, run ExportToZoomTracks.py
  - This should generate an FBX model file and a JSON file with collision data
- Open the ZoomTracks project in Unity Editor
-
