#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class String_TruncateEnd {
        public static string TruncateEnd(this string s, int length) {
            return s.Substring(0, s.Length - length);
        }
    }
}
