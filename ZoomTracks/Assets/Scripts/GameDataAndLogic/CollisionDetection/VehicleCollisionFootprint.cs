using System;
using UnityEngine;

namespace ZoomTracks {
    /// <summary>
    /// The axis-aligned footprint of a vehicle's render meshes in vehicle-local
    /// X/Z space. It is derived once when a car becomes active, so collision
    /// geometry follows the actual imported asset rather than a hand-maintained
    /// duplicate size.
    /// </summary>
    public readonly struct VehicleCollisionFootprint {
        private VehicleCollisionFootprint(float minX, float minZ, float maxX, float maxZ) {
            this.MinX = minX;
            this.MinZ = minZ;
            this.MaxX = maxX;
            this.MaxZ = maxZ;
        }

        public float MinX { get; }
        public float MinZ { get; }
        public float MaxX { get; }
        public float MaxZ { get; }

        public static VehicleCollisionFootprint FromMeshGeometry(Transform vehicleRoot) {
            if (vehicleRoot == null) {
                throw new ArgumentNullException(nameof(vehicleRoot));
            }

            bool foundBounds = false;
            float minX = float.PositiveInfinity;
            float minZ = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxZ = float.NegativeInfinity;

            MeshFilter[] meshFilters = vehicleRoot.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            for (int i = 0; i < meshFilters.Length; ++i) {
                MeshFilter meshFilter = meshFilters[i];
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null) {
                    continue;
                }

                Matrix4x4 meshToVehicle = vehicleRoot.worldToLocalMatrix
                    * meshFilter.transform.localToWorldMatrix;
                IncludeBounds(
                    mesh.bounds,
                    meshToVehicle,
                    ref foundBounds,
                    ref minX,
                    ref minZ,
                    ref maxX,
                    ref maxZ);
            }

            SkinnedMeshRenderer[] skinnedRenderers =
                vehicleRoot.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            for (int i = 0; i < skinnedRenderers.Length; ++i) {
                SkinnedMeshRenderer renderer = skinnedRenderers[i];
                if (renderer.sharedMesh == null) {
                    continue;
                }

                Matrix4x4 meshToVehicle = vehicleRoot.worldToLocalMatrix
                    * renderer.transform.localToWorldMatrix;
                IncludeBounds(
                    renderer.localBounds,
                    meshToVehicle,
                    ref foundBounds,
                    ref minX,
                    ref minZ,
                    ref maxX,
                    ref maxZ);
            }

            if (!foundBounds) {
                throw new InvalidOperationException(
                    $"Vehicle '{vehicleRoot.name}' has no mesh geometry from which to derive collision bounds.");
            }

            if (!(minX < maxX) || !(minZ < maxZ)) {
                throw new InvalidOperationException(
                    $"Vehicle '{vehicleRoot.name}' does not have a positive-area X/Z mesh footprint.");
            }

            return new VehicleCollisionFootprint(minX, minZ, maxX, maxZ);
        }

        public RectangleLocalBounds GetScaledLocalBounds(Transform vehicleRoot) {
            if (vehicleRoot == null) {
                throw new ArgumentNullException(nameof(vehicleRoot));
            }

            // CarState applies a pure yaw and the game uses positive, axis-aligned
            // vehicle scale. Vector magnitudes retain that scale without allowing
            // the current yaw to inflate the local rectangle into a world AABB.
            float xScale = vehicleRoot.TransformVector(Vector3.right).magnitude;
            float zScale = vehicleRoot.TransformVector(Vector3.forward).magnitude;
            if (!Guard.IsFinite(xScale)
                || !Guard.IsFinite(zScale)
                || !(xScale > 0f)
                || !(zScale > 0f)) {
                throw new InvalidOperationException(
                    $"Vehicle '{vehicleRoot.name}' must have finite positive planar scale.");
            }

            float x0 = this.MinX * xScale;
            float x1 = this.MaxX * xScale;
            float z0 = this.MinZ * zScale;
            float z1 = this.MaxZ * zScale;
            return new RectangleLocalBounds(
                Math.Min(x0, x1),
                Math.Min(z0, z1),
                Math.Max(x0, x1),
                Math.Max(z0, z1));
        }

        private static void IncludeBounds(
            Bounds bounds,
            Matrix4x4 meshToVehicle,
            ref bool foundBounds,
            ref float minX,
            ref float minZ,
            ref float maxX,
            ref float maxZ) {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int xSign = -1; xSign <= 1; xSign += 2) {
                for (int ySign = -1; ySign <= 1; ySign += 2) {
                    for (int zSign = -1; zSign <= 1; zSign += 2) {
                        Vector3 meshPoint = center + Vector3.Scale(
                            extents,
                            new Vector3(xSign, ySign, zSign));
                        Vector3 vehiclePoint = meshToVehicle.MultiplyPoint3x4(meshPoint);
                        if (!Guard.IsFinite(vehiclePoint.x) || !Guard.IsFinite(vehiclePoint.z)) {
                            throw new InvalidOperationException(
                                "Vehicle mesh bounds produced a nonfinite local footprint.");
                        }

                        minX = Math.Min(minX, vehiclePoint.x);
                        minZ = Math.Min(minZ, vehiclePoint.z);
                        maxX = Math.Max(maxX, vehiclePoint.x);
                        maxZ = Math.Max(maxZ, vehiclePoint.z);
                        foundBounds = true;
                    }
                }
            }
        }
    }
}
