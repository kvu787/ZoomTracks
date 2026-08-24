using System;

namespace ZoomTracks.CollisionDetection {
    public readonly struct CoordinateXY {
        public CoordinateXY(float x, float y) {
            Guard.ThrowIfNotFinite(x, nameof(x));
            Guard.ThrowIfNotFinite(y, nameof(y));
            this.X = x;
            this.Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    public interface ICollisionDetector {
        bool IsColliding(RectangleLocalBounds localBounds, RectanglePose pose);
    }

    public readonly struct RectangleLocalBounds {
        public RectangleLocalBounds(float minX, float minY, float maxX, float maxY) {
            Guard.ThrowIfNotFinite(minX, nameof(minX));
            Guard.ThrowIfNotFinite(minY, nameof(minY));
            Guard.ThrowIfNotFinite(maxX, nameof(maxX));
            Guard.ThrowIfNotFinite(maxY, nameof(maxY));

            if (!(minX < maxX)) {
                throw new ArgumentException("minX must be less than maxX.");
            }

            if (!(minY < maxY)) {
                throw new ArgumentException("minY must be less than maxY.");
            }

            this.MinX = minX;
            this.MinY = minY;
            this.MaxX = maxX;
            this.MaxY = maxY;
        }

        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }

        internal bool IsValid => Guard.IsFinite(this.MinX)
                    && Guard.IsFinite(this.MinY)
                    && Guard.IsFinite(this.MaxX)
                    && Guard.IsFinite(this.MaxY)
                    && this.MinX < this.MaxX
                    && this.MinY < this.MaxY;
    }

    public readonly struct RectanglePose {
        public RectanglePose(float positionX, float positionY, float rotationDegrees) {
            Guard.ThrowIfNotFinite(positionX, nameof(positionX));
            Guard.ThrowIfNotFinite(positionY, nameof(positionY));
            Guard.ThrowIfNotFinite(rotationDegrees, nameof(rotationDegrees));
            this.PositionX = positionX;
            this.PositionY = positionY;
            this.RotationDegrees = rotationDegrees;
        }

        public float PositionX { get; }
        public float PositionY { get; }
        public float RotationDegrees { get; }
    }

    internal static class Guard {
        internal static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static void ThrowIfNotFinite(float value, string parameterName) {
            if (!IsFinite(value)) {
                throw new ArgumentOutOfRangeException(parameterName, "The value must be finite.");
            }
        }
    }
}
