using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.Common;
using Windows.Storage;

namespace ComicsViewer.Uwp.Common {
    public static class StorageItem_GetItemsInNaturalOrder {
        public static async Task<IReadOnlyList<StorageFile>> GetFilesInNaturalOrderAsync(this StorageFolder item) {
            return InNaturalOrder(await item.GetFilesAsync());
        }

        public static async Task<IReadOnlyList<StorageFolder>> GetFoldersInNaturalOrderAsync(this StorageFolder item) {
            return InNaturalOrder(await item.GetFoldersAsync());
        }

        public static async Task<IReadOnlyList<IStorageItem>> GetItemsInNaturalOrderAsync(this StorageFolder item) {
            return InNaturalOrder(await item.GetItemsAsync());
        }

        public static IReadOnlyList<R> InNaturalOrder<R>(this IEnumerable<R> list) where R: IStorageItem {
            var items = list.ToList();
            items.Sort((left, right) => NaturalOrder.Comparer.Compare(left.Name, right.Name));
            return items;
        }
    }
}
