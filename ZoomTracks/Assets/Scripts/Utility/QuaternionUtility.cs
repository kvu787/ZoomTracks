using UnityEngine;

namespace ZoomTracks {
    public static class QuaterionUtility {
        /// <summary>
        /// A pure world-Y rotation keeps a flat (y == 0) vector exactly on the XZ
        /// plane, because the quaternion's x and z components are exactly 0. The
        /// floating-point error lives in the IN-PLANE quantities (magnitude /
        /// round-trip / accumulation), not in y.
        /// </summary>
        public static void DemonstrateFloatingPointError() {
            Vector3 v = new(1.5f, 0f, 2.5f);

            float maxYError = 0f; // structurally protected -> stays 0
            float maxMagnitudeError = 0f; // rotation should preserve length, but won't
            float maxRoundTripMagnitudeError = 0f; // R(-a) * R(a) * v should land back on v

            for (int angle = 1; angle <= 360; angle++) {
                Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
                Vector3 rotatedV = rotation * v;

                maxYError = Mathf.Max(maxYError, Mathf.Abs(rotatedV.y));
                maxMagnitudeError = Mathf.Max(maxMagnitudeError, Mathf.Abs(rotatedV.magnitude - v.magnitude));

                Vector3 roundTripV = Quaternion.Euler(0f, -angle, 0f) * rotatedV;
                maxRoundTripMagnitudeError = Mathf.Max(maxRoundTripMagnitudeError, (roundTripV - v).magnitude);
            }

            // 360 x 1 deg steps -> analytically a full turn back to v.
            Vector3 acc = v;
            Quaternion step = Quaternion.Euler(0f, 1f, 0f);
            for (int i = 0; i < 360; i++) {
                acc = step * acc;
            }

            float accumulatedDrift = (acc - v).magnitude;

            Debug.Log(
                $"Floating-point errors:\n" +
                $"Max |y| error (provably 0):                {maxYError.ToExactDecimalString()}\n" +
                $"Max magnitude error for (single rotation): {maxMagnitudeError.ToExactDecimalString()}\n" +
                $"Max round-trip drift R(-a)*R(a)*v vs v:    {maxRoundTripMagnitudeError.ToExactDecimalString()}\n" +
                $"Accumulated drift after 360 x 1 deg steps: {accumulatedDrift.ToExactDecimalString()}");
        }
    }
}
