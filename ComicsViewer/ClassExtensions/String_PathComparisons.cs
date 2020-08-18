using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class String_PathComparisons {
        /// <summary>
        /// Tests whether <c>child</c> is <c>parent</c> or a subdirectory in <c>parent</c>.
        /// </summary>
        public static bool IsChildOfDirectory(this string child, string parent) {
            return Path.GetFullPath(child).StartsWith(Path.GetFullPath(parent), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// <list type="table">
        /// <item>If <c>path</c> is a subdirectory of <c>parent</c>, returns the relative path of <c>path</c> to <c>parent</c>.</item>
        /// <item>If <c>path</c> is <c>parent</c>, returns an empty string.</item>
        /// <item>Otherwise, returns the absolute path of <c>path</c>.</item>
        /// </list>
        /// </summary>
        public static string GetPathRelativeTo(this string path, string parent) {
            path = Path.GetFullPath(path);
            parent = Path.GetFullPath(parent);

            if (!path.IsChildOfDirectory(parent)) {
                return path;
            }

            if (path.Equals(parent, StringComparison.OrdinalIgnoreCase)) {
                return "";
            }

            return path.Substring(parent.Length + 1);
        }
    }
}
