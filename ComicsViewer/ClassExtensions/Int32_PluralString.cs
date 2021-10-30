#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class Int32_PluralString {
        public static string PluralString(this int i, string counter, string pluralSuffix = "s", bool simple = false) {
            if (simple && i == 1) {
                return counter;
            }

            if (i == 1) {
                return $"{i} {counter}";
            }

            return $"{i} {counter}{pluralSuffix}";
        }
    }
}
