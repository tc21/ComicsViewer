using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class IEnumerable_FirstOrNull {
        public static T? FirstOrNull<T>(this IEnumerable<T> enumerable) where T : struct {
            try {
                return enumerable.First();
            } catch (InvalidOperationException) {
                return null;
            }
        }
    }
}
