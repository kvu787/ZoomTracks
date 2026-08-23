using System.Collections.Generic;

namespace ZoomTracks.CollisionDetection
{
    /// <summary>
    /// Directly scans every outline segment for each potentially overlapping query edge.
    /// This minimizes construction cost and has very small constants.
    /// </summary>
    public sealed class LinearScanIndex : OutlineIndexBase
    {
        public LinearScanIndex(
            IReadOnlyList<FloatPoint> outline1,
            IReadOnlyList<FloatPoint> outline2)
            : base(outline1, outline2)
        {
        }

        public override bool Intersects(QueryPerimeter perimeter)
        {
            if (!perimeter.Bounds.Overlaps(OutlineBounds))
            {
                return false;
            }

            for (int edgeIndex = 0; edgeIndex < 4; ++edgeIndex)
            {
                FloatPoint queryA = perimeter.GetVertex(edgeIndex);
                FloatPoint queryB = perimeter.GetVertex((edgeIndex + 1) & 3);
                Aabb queryBounds = Aabb.FromSegment(queryA, queryB);
                if (!queryBounds.Overlaps(OutlineBounds))
                {
                    continue;
                }

                for (int segmentIndex = 0; segmentIndex < Segments.Length; ++segmentIndex)
                {
                    OutlineSegment segment = Segments[segmentIndex];
                    if (ExactSegmentPredicates.IntersectsWithKnownBounds(
                        queryA,
                        queryB,
                        queryBounds,
                        segment.A,
                        segment.B,
                        segment.Bounds))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
