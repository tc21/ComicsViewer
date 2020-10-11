using System;
using System.Collections.Generic;

#nullable enable

namespace ComicsLibrary.Sorting {
    public static class General {
        internal static readonly Random Randomizer = new Random();

        /// <summary>
        /// Shuffles a list in-place in O(N) time.
        /// </summary>
        public static void Shuffle<T>(List<T> list) {
            // Fisher-Yates
            for (var i = list.Count - 1; i > 0; i--) {
                var random = Randomizer.Next(i + 1);
                var temp = list[i];
                list[i] = list[random];
                list[random] = temp;
            }
        }
    }
}
