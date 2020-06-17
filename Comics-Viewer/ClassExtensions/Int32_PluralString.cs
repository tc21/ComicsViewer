using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class Int32_PluralString {
        public static string PluralString(this int i, string counter, string pluralSuffix = "s") {
            if (i == 1) {
                return $"{i} {counter}";
            }

            return $"{i} {counter}{pluralSuffix}";
        }
    }
}
