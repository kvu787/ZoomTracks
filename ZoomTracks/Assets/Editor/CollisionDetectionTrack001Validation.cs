using System;
using UnityEditor;
using UnityEngine;

namespace ZoomTracks {
    public static class CollisionDetectionTrack001Validation {
        private const string TrackModelAssetPath = "Assets/FBX/Track001.fbx";
        private const string ColliderDataFileName = "Track001_ColliderData.json";
        private const float TransformTolerance = 0.001f;
        private const float BoundsTolerance = 0.001f;

        [MenuItem("Tools/Collision Detection/Validate Track001")]
        private static void RunFromMenu() {
            Execute();
        }

        /// <summary>
        /// Validates the production collision pipeline against the actual imported
        /// Track001 FBX and collider-data JSON. This method is public so Unity can
        /// invoke it with -executeMethod from a batch validation command.
        /// </summary>
        public static void Execute() {
            GameObject trackRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                TrackModelAssetPath);
            Require(trackRoot != null, $"Could not load '{TrackModelAssetPath}'.");
            ValidateRootTransform(trackRoot.transform);

            Transform placeholder = FindUniqueDescendant(
                trackRoot.transform,
                "SlopeCarPlaceholder");
            ValidatePlaceholderTransform(placeholder);

            Transform blueCar = FindUniqueDescendant(trackRoot.transform, "SlopeCarBlue");
            VehicleCollisionFootprint footprint =
                VehicleCollisionFootprint.FromMeshGeometry(blueCar);
            ValidateFootprint(footprint);
            RectangleLocalBounds vehicleBounds = footprint.GetScaledLocalBounds(blueCar);

            ColliderJson colliderJson =
                JsonUtility.Deserialize<ColliderJson>(ColliderDataFileName);
            Require(colliderJson != null, "Track001 collider JSON deserialized to null.");

            TrackCollisionDetector detector = new(colliderJson, vehicleBounds);
            Require(detector.OutlineCount == 3, $"Expected 3 outlines, got {detector.OutlineCount}.");
            Require(detector.EdgeCount == 1984, $"Expected 1984 edges, got {detector.EdgeCount}.");
            Require(
                detector.OrdinaryEdgeCount == 1984,
                $"Expected all 1984 edges to be ordinary, got {detector.OrdinaryEdgeCount}.");
            Require(
                detector.OutlierEdgeCount == 0,
                $"Expected no outlier edges, got {detector.OutlierEdgeCount}.");
            Require(
                detector.StoredGridEdgeReferenceCount == 1984,
                "The center grid must store every Track001 edge exactly once.");
            RequireApproximately(
                detector.CellSize,
                3.0,
                BoundsTolerance,
                "Track001 grid cell size");

            CoordinateXY rawFirstVertex = colliderJson.Outlines[0].Vertices[0];
            float mappedX = -rawFirstVertex.X;
            float mappedY = -rawFirstVertex.Y;
            RectangleLocalBounds cornerBounds = new(0f, 0f, 1f, 1f);
            Require(
                detector.IsColliding(
                    cornerBounds,
                    new RectanglePose(mappedX, mappedY, 0f)),
                "A rectangle corner at the first (-Blender X, -Blender Y) outline vertex "
                + "must collide.");

            Require(
                !detector.IsColliding(
                    vehicleBounds,
                    new RectanglePose(1_000_000f, 1_000_000f, 0f)),
                "A far-away vehicle rectangle must not collide.");
            Require(
                !detector.IsColliding(
                    new RectangleLocalBounds(-1000f, -1000f, 1000f, 1000f),
                    new RectanglePose(0f, 0f, 0f)),
                "A rectangle whose perimeter encloses the track without touching it "
                + "must not collide.");

            Debug.Log(
                "PASS: Track001 collision validation. "
                + $"outlines={detector.OutlineCount}, edges={detector.EdgeCount}, "
                + $"ordinary={detector.OrdinaryEdgeCount}, cellSize={detector.CellSize:R}, "
                + $"occupiedCells={detector.OccupiedGridCellCount}, "
                + $"firstVertexUnity=({mappedX:R}, {mappedY:R}).");
        }

        private static void ValidateRootTransform(Transform root) {
            RequireApproximately(root.localPosition.x, 0f, TransformTolerance, "root position X");
            RequireApproximately(root.localPosition.y, 0f, TransformTolerance, "root position Y");
            RequireApproximately(root.localPosition.z, 0f, TransformTolerance, "root position Z");
            Require(
                Quaternion.Angle(root.localRotation, Quaternion.identity) <= TransformTolerance,
                $"Track root rotation must be identity, got {root.localRotation}.");
            RequireApproximately(root.localScale.x, 1f, TransformTolerance, "root scale X");
            RequireApproximately(root.localScale.y, 1f, TransformTolerance, "root scale Y");
            RequireApproximately(root.localScale.z, 1f, TransformTolerance, "root scale Z");
        }

        private static void ValidatePlaceholderTransform(Transform placeholder) {
            RequireApproximately(
                placeholder.position.x,
                -63.598217f,
                TransformTolerance,
                "placeholder world X");
            RequireApproximately(
                placeholder.position.y,
                0f,
                TransformTolerance,
                "placeholder world Y");
            RequireApproximately(
                placeholder.position.z,
                -31.660368f,
                TransformTolerance,
                "placeholder world Z");
            RequireApproximately(
                Mathf.DeltaAngle(placeholder.rotation.eulerAngles.y, 102.852995f),
                0f,
                TransformTolerance,
                "placeholder yaw");
        }

        private static void ValidateFootprint(VehicleCollisionFootprint footprint) {
            RequireApproximately(footprint.MinX, -1.5f, BoundsTolerance, "vehicle min X");
            RequireApproximately(footprint.MaxX, 1.5f, BoundsTolerance, "vehicle max X");
            RequireApproximately(footprint.MinZ, -3.0f, BoundsTolerance, "vehicle min Z");
            RequireApproximately(
                footprint.MaxZ,
                3.157522f,
                BoundsTolerance,
                "vehicle max Z");
            Require(
                Math.Abs(footprint.MinZ + footprint.MaxZ) > 0.1f,
                "The imported vehicle footprint must preserve its asymmetric local Z bounds.");
        }

        private static Transform FindUniqueDescendant(Transform root, string objectName) {
            Transform result = null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < transforms.Length; ++i) {
                if (!string.Equals(transforms[i].name, objectName, StringComparison.Ordinal)) {
                    continue;
                }

                Require(result == null, $"Found more than one '{objectName}' in Track001.fbx.");
                result = transforms[i];
            }

            Require(result != null, $"Could not find '{objectName}' in Track001.fbx.");
            return result;
        }

        private static void RequireApproximately(
            double actual,
            double expected,
            double tolerance,
            string description) {
            Require(
                Math.Abs(actual - expected) <= tolerance,
                $"Expected {description} to be {expected:R} +/- {tolerance:R}, got {actual:R}.");
        }

        private static void Require(bool condition, string message) {
            if (!condition) {
                throw new InvalidOperationException($"Track001 collision validation failed: {message}");
            }
        }
    }
}
