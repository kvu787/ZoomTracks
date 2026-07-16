using System;
using System.Globalization;
using System.Numerics;

namespace ZoomTracks {
    public static class FloatExtensions {
        public static string ToExactDecimalString(this float value, bool preserveNegativeZero = false) {
            uint bits = SingleToUInt32BitsUnity(value);

            bool negative = (bits & 0x80000000u) != 0;
            int exponentBits = (int)((bits >> 23) & 0xffu);
            uint fractionBits = bits & 0x7fffffu;

            // Special IEEE-754 values do not have finite decimal representations.
            if (exponentBits == 0xff) {
                if (fractionBits != 0) {
                    return "NaN";
                }

                return negative ? "-Infinity" : "Infinity";
            }

            // Zero, including negative zero.
            if (exponentBits == 0 && fractionBits == 0) {
                return negative && preserveNegativeZero ? "-0" : "0";
            }

            BigInteger significand;
            int binaryExponent;

            if (exponentBits == 0) {
                // Subnormal:
                // value = fractionBits * 2^-149
                significand = fractionBits;
                binaryExponent = -149;
            } else {
                // Normal:
                // value = (2^23 + fractionBits) * 2^(exponentBits - 150)
                significand = (1u << 23) | fractionBits;
                binaryExponent = exponentBits - 150;
            }

            string sign = negative ? "-" : "";

            if (binaryExponent >= 0) {
                BigInteger integerValue = significand << binaryExponent;
                return sign + integerValue.ToString(CultureInfo.InvariantCulture);
            }

            int decimalPlaces = -binaryExponent;

            // Remove factors of 2 from the numerator to avoid redundant
            // trailing decimal zeroes.
            while (decimalPlaces > 0 && significand.IsEven) {
                significand >>= 1;
                decimalPlaces--;
            }

            if (decimalPlaces == 0) {
                return sign + significand.ToString(CultureInfo.InvariantCulture);
            }

            // significand / 2^decimalPlaces
            // = significand * 5^decimalPlaces / 10^decimalPlaces
            BigInteger scaled = significand * BigInteger.Pow(5, decimalPlaces);
            string digits = scaled.ToString(CultureInfo.InvariantCulture);

            if (digits.Length <= decimalPlaces) {
                return sign
                    + "0."
                    + new string('0', decimalPlaces - digits.Length)
                    + digits;
            }

            int pointIndex = digits.Length - decimalPlaces;
            return sign + digits.Insert(pointIndex, ".");
        }

        // This is a Unity 6.3 / .NET Standard 2.1 compatibility replacement for:
        //     BitConverter.SingleToUInt32Bits(value)
        //
        // It is not a numeric float-to-uint conversion.
        //
        // Why this is necessary:
        // Unity 6.3 defaults its Player API Compatibility Level to .NET Standard 2.1.
        // In that profile, BitConverter.SingleToUInt32Bits may not be available/reliable across
        // Unity targets, so use the older SingleToInt32Bits API and cast unchecked to preserve
        // the exact 32-bit IEEE-754 float bit pattern.
        private static uint SingleToUInt32BitsUnity(float value) {
            return unchecked((uint)BitConverter.SingleToInt32Bits(value));
        }
    }
}
