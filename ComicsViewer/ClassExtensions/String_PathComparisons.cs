using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class String_PathComparisons {
        public static bool IsChildOfDirectory(this string child, string parent) {
            return Path.GetFullPath(child).StartsWith(Path.GetFullPath(parent), StringComparison.OrdinalIgnoreCase);
        }

        public static string GetPathRelativeTo(this string path, string parent) {
            if (!path.IsChildOfDirectory(parent)) {
                return path;
            }

            if (path == parent) {
                return "";
            }

            return Path.GetFullPath(path).Substring(Path.GetFullPath(parent).Length + 1);
        }
    }
}
