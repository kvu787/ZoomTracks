using UnityEngine;

namespace ZoomTracks {
    public struct TransformStruct {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

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
