using System;
using System.Collections.Generic;
using System.Linq;
using ComicsViewer.ViewModels;

#nullable enable

namespace ComicsViewer.Support {
    public static class ComicItemGridCache {
        private static readonly List<(NavigationTag, string, List<ComicItem>)> stack = new();
        private static readonly DefaultDictionary<NavigationTag, List<ComicItem>?> roots = new(() => null);

        /* A brief description of how to use this Cache:
         * I. Root pages
         *  1. Navigating in: GetRoot ?? generate
         *  2. Navigating out: PutRoot
         *  
         * II. Nonroot pages
         *  1. Navigating in, creating new page: PruneStack, generate
         *  2. Navigating in, moving forwards: GetStack
         *  3. Navigating in, moving backwards: GetStack
         *  4. Navigating out: PutStack
         *  
         * (note: behavior is different while forward navigation is not yet implemented) */

        public static List<ComicItem>? GetRoot(NavigationTag tag) {
            return roots[tag];
        }

        public static void PutRoot(NavigationTag tag, IEnumerable<ComicItem> items) {
            roots[tag] = items.ToList();
        }

        public static void PushStack(NavigationTag tag, string subKey, IEnumerable<ComicItem> items) {
            stack.Add((tag, subKey, items.ToList()));
        }

        public static List<ComicItem> PopStack(NavigationTag tag, string? subKey) {
            var index = stack.Count - 1;
            var (storedTag, storedSubKey, items) = stack[index];

            if ((storedTag, storedSubKey) != (tag, subKey)) {
                throw new ArgumentException("Item at top of stack was not the expected (key, subkey) combination");
            }

            return items;
        }

        public static void PruneStack(int itemsToKeep) {
            while (stack.Count > itemsToKeep) {
                stack.RemoveAt(itemsToKeep);
            }
        }

        public static void Clear() {
            stack.Clear();
            roots.Clear();
        }
    }
}
