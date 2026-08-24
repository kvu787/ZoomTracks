using System.Collections.Generic;

namespace ZoomTracks.CollisionDetection {
    /// <summary>
    /// Exact detector with minimal preprocessing and auxiliary storage.
    /// </summary>
    public sealed class LinearScanCollisionDetector : CollisionDetectorBase {
        public LinearScanCollisionDetector(
            List<CoordinateXY> outline1,
            List<CoordinateXY> outline2)
            : base(outline1, outline2) {
        }

        private protected override bool Query(in RectangleQuad rectangle) {
            for (int edgeIndex = 0; edgeIndex < this.EdgeCount; ++edgeIndex) {
                if (this.EdgeIntersectsRectangle(edgeIndex, rectangle)) {
                    return true;
                }
            }

            return false;
        }
    }
}
