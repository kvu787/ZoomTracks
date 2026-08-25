using System;
using System.Collections.Generic;

namespace ZoomTracks {
    /// <summary>
    /// Track-outline collision data exported from Blender. Version 1 coordinates
    /// are Blender world-space X/Y values; the runtime converts them to Unity's
    /// ground plane as (-X, -Y) -> (world X, world Z).
    /// </summary>
    [Serializable]
    public class ColliderJson {
        public const int CurrentFormatVersion = 1;
        public const string BlenderWorldXYCoordinateSystem = "BlenderWorldXY";

        public int FormatVersion;
        public string CoordinateSystem;
        public List<Outline> Outlines;
    }
}
