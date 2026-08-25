using UnityEngine;

namespace ZoomTracks {
    /// <summary>
    /// Connects the active Unity vehicle to the immutable track-outline index.
    /// Track geometry is rebuilt only when a track is initialized; vehicle mesh
    /// bounds are refreshed only when the active car changes.
    /// </summary>
    public sealed class CollisionManager2 {
        private const float ShortenColliderFront = 0.75f;
        private const float ShortenColliderRear = 1.3f;

        private readonly CarSwitcher _carSwitcher;
        private readonly ColliderJson _colliderJson;

        private Transform _currentVehicle;
        private VehicleCollisionFootprint _currentFootprint;
        private TrackCollisionDetector _detector;

        public CollisionManager2(string trackName, CarSwitcher carSwitcher) {
            if (string.IsNullOrEmpty(trackName)) {
                throw new System.ArgumentException("A track name is required.", nameof(trackName));
            }

            this._carSwitcher = carSwitcher
                ?? throw new System.ArgumentNullException(nameof(carSwitcher));
            this._colliderJson = JsonUtility.Deserialize<ColliderJson>(
                $"{trackName}_ColliderData.json");
            this.RefreshCurrentVehicleIfNeeded();
        }

        public bool IsCarColliding() {
            this.RefreshCurrentVehicleIfNeeded();

            RectangleLocalBounds bounds = this.GetCurrentVehicleBounds();
            Vector3 position = this._currentVehicle.position;
            RectanglePose pose = new(
                position.x,
                position.z,
                this._currentVehicle.rotation.eulerAngles.y);
            return this._detector.IsColliding(bounds, pose);
        }

        private void RefreshCurrentVehicleIfNeeded() {
            Transform currentVehicle = this._carSwitcher.CurrentCarTransform;
            if (ReferenceEquals(currentVehicle, this._currentVehicle)) {
                return;
            }

            this._currentVehicle = currentVehicle;
            this._currentFootprint = VehicleCollisionFootprint.FromMeshGeometry(currentVehicle);
            RectangleLocalBounds representativeBounds = this.GetCurrentVehicleBounds();

            // Rebuild only when the active vehicle changes scale materially. The
            // index remains correct for any query size; this keeps its cell scale
            // aligned with a very different vehicle without rebuilding for tiny
            // importer roundoff differences between otherwise identical cars.
            float shortExtent = Mathf.Min(
                representativeBounds.MaxX - representativeBounds.MinX,
                representativeBounds.MaxY - representativeBounds.MinY);
            if (this._detector == null
                || shortExtent < this._detector.CellSize * 0.75f
                || shortExtent > this._detector.CellSize * 1.25f) {
                this._detector = new TrackCollisionDetector(
                    this._colliderJson,
                    representativeBounds);
                Debug.Log(
                    $"Built track collision index: edges={this._detector.EdgeCount}, "
                    + $"cellSize={this._detector.CellSize}, "
                    + $"gridCells={this._detector.GridCellCount}, "
                    + $"occupiedCells={this._detector.OccupiedCellCount}, "
                    + $"oversizedEdges={this._detector.OversizedEdgeCount}, "
                    + $"denseGrid={this._detector.UsesDenseGrid}");
            }
        }

        private RectangleLocalBounds GetCurrentVehicleBounds() {
            RectangleLocalBounds bounds =
                this._currentFootprint.GetScaledLocalBounds(this._currentVehicle);
            return new RectangleLocalBounds(
                bounds.MinX,
                bounds.MinY + ShortenColliderRear,
                bounds.MaxX,
                bounds.MaxY - ShortenColliderFront);
        }
    }
}
