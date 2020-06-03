using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.ClassExtensions {
    public static class String_TruncateEnd {
        public static string TruncateEnd(this string s, int length) {
            return s.Substring(0, s.Length - length);
        }
    }
}
