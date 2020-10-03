using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ComicsViewer.Common {
    public static class NaturalOrder {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string psz1, string psz2);
        public static IComparer<string> Comparer = new NaturalOrderComparer();

        private class NaturalOrderComparer : IComparer<string> {
            public int Compare(string x, string y) {
                return StrCmpLogicalW(x, y);
            }
        }
    }
}