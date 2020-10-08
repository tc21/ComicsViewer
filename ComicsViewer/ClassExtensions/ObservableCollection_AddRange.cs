using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class ObservableCollection_AddRange {
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items) {
            foreach (var item in items) {
                collection.Add(item);
            }
        }
    }
}
