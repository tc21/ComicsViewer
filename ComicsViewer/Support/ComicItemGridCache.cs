using System;
using System.Collections.Generic;
using ComicsViewer.ViewModels;

#nullable enable

namespace ComicsViewer.Support {
    public class ComicItemGridState {
        public List<ComicItem> Items { get; set; }
        public double ScrollOffset { get; set; }
        public DateTime LastModified { get; set; }

        public ComicItemGridState(List<ComicItem> items, double scrollOffset, DateTime lastModified) {
            this.Items = items;
            this.ScrollOffset = scrollOffset;
            this.LastModified = lastModified;
        }
    }

    public static class ComicItemGridCache {
        private static readonly List<(NavigationTag, string, ComicItemGridState)> stack = new();
        private static readonly DefaultDictionary<NavigationTag, ComicItemGridState?> roots = new(() => null);

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

        public static ComicItemGridState? GetRoot(NavigationTag tag) {
            ignorePutsUntilNextGet = false;
            return roots[tag];
        }

        public static void PutRoot(NavigationTag tag, ComicItemGridState state) {
            if (ignorePutsUntilNextGet) {
                return;
            }

            roots[tag] = state;
        }

        public static void PushStack(NavigationTag tag, string subKey, ComicItemGridState state) {
            stack.Add((tag, subKey, state));
        }

        public static ComicItemGridState PopStack(NavigationTag tag, string? subKey) {
            var index = stack.Count - 1;
            var (storedTag, storedSubKey, state) = stack[index];

            RemoveStackItemAt(index);

            if ((storedTag, storedSubKey) != (tag, subKey)) {
                throw new ArgumentException("Item at top of stack was not the expected (key, subkey) combination");
            }

            return state;
        }

        public static void PruneStack(int itemsToKeep) {
            while (stack.Count > itemsToKeep) {
                RemoveStackItemAt(itemsToKeep);
            }
        }

        private static void RemoveStackItemAt(int index) {
            stack.RemoveAt(index);
        }

        public static void Clear() {
            stack.Clear();
            roots.Clear();
        }

        private static bool ignorePutsUntilNextGet = false;

        // We don't have a reliable way of disabling the cache when we switch profiles, 
        // so this is the workaround.
        public static void IgnorePutsUntilNextGet() {
            ignorePutsUntilNextGet = true;
        }
    }
}
