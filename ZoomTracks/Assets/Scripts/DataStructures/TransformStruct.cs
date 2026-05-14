using UnityEngine;

namespace ZoomTracks {
    public readonly struct TransformStruct {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }

        public TransformStruct(Transform t) {
            this.Position = t.position;
            this.Rotation = t.rotation;
            this.Scale = t.localScale;
        }

        public readonly void ApplyTo(Transform transform) {
            transform.SetPositionAndRotation(this.Position, this.Rotation);
            transform.localScale = this.Scale;
        }
    }
}
