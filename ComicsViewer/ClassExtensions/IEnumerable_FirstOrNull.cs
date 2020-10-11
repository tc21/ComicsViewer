using System.Collections.Generic;

#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class IEnumerable_FirstOrNull {
        public static T? FirstOrNull<T>(this IEnumerable<T> enumerable) where T : class {
            using var enumerator = enumerable.GetEnumerator();

            return enumerator.MoveNext()
                ? enumerator.Current
                : null;
        }
    }
}
