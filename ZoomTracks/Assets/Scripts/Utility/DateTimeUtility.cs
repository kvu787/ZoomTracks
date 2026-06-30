using System;
using System.Globalization;

namespace ZoomTracks {
    public static class DateTimeUtility {
        private static readonly char[] Chars = new char[22];
        private const string Format = "yyyy'/'MM'/'dd hh':'mm':'ss tt";

        public static char[] GetDateTimeNowChars_Buffered() {
            DateTime now = DateTime.Now;
            bool success = now.TryFormat(Chars.AsSpan(), out int numCharsWritten, Format.AsSpan(), CultureInfo.InvariantCulture);
            if (!success || numCharsWritten != Chars.Length) {
                throw new Exception($"Date/time format did not fit the static buffer (size={Chars.Length}.)");
            }
            return Chars;
        }
    }
}
