using System;

namespace ZoomTracks {
    public static class EnumExtensions {
        // The CLR creates a unique version of this class for every type T.
        // The static constructor runs exactly once per Enum type.
        private static class EnumCache<T> where T : Enum {
            // Use typeof(T) and cast to T[] for Unity / older .NET versions
            public static readonly T[] Values = (T[])Enum.GetValues(typeof(T));
        }

        public static T Next<T>(this T value) where T : Enum {
            T[] values = EnumCache<T>.Values;
            int i = Array.IndexOf(values, value);

            return values[(i + 1) % values.Length];
        }

        public static T Previous<T>(this T value) where T : Enum {
            T[] values = EnumCache<T>.Values;
            int i = Array.IndexOf(values, value);

            return values[(i - 1 + values.Length) % values.Length];
        }
    }
}
