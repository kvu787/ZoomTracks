using System;
using UnityEngine;

namespace ZoomTracks {
    public class InputUtility {
        public static float AxialDeadzone(float value, float innerDeadzone, float outerDeadzone) {
            if (!(0f <= innerDeadzone && innerDeadzone <= 1f)) {
                throw new ArgumentException($"Expected: innerDeadzone must be in [0, 1]. Got: innerDeadzone={innerDeadzone}.");
            }
            if (!(0f <= outerDeadzone && outerDeadzone <= 1f)) {
                throw new ArgumentException($"Expected: outerDeadzone must be in [0, 1]. Got: outerDeadzone={outerDeadzone}.");
            }
            if (innerDeadzone >= outerDeadzone) {
                throw new ArgumentException($"Expected: innerDeadzone must be less than outerDeadzone. Got: innerDeadzone={innerDeadzone}, outerDeadzone={outerDeadzone}.");
            }

            if (value == 0f) {
                return 0f;
            }

            float magnitude = Mathf.Abs(value);
            if (magnitude < innerDeadzone) {
                return 0f;
            } else {
                float sign = Mathf.Sign(value);
                if (magnitude > outerDeadzone) {
                    return sign;
                } else {
                    return sign * ((magnitude - innerDeadzone) / (outerDeadzone - innerDeadzone));
                }
            }
        }
    }
}
