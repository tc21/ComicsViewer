#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class Int32_PluralString {
        public static string PluralString(this int i, string counter, string pluralSuffix = "s") {
            return i == 1
                ? $"{i} {counter}"
                : $"{i} {counter}{pluralSuffix}";
        }
    }
}
