#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class Int32_PluralString {
        public static string PluralString(this int i, string counter, string pluralSuffix = "s", bool simple = false) {
            return i == 1
                ? (simple ? counter : $"{i} {counter}")
                : $"{i} {counter}{pluralSuffix}";
        }
    }
}
