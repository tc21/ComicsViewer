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

        public static IReadOnlyList<T> InNaturalOrder<T>(this IEnumerable<T> list) where T: IStorageItem {
            var items = list.ToList();
            items.Sort((left, right) => NaturalOrder.Comparer.Compare(left.Name, right.Name));
            return items;
        }

        public static IReadOnlyList<Win32Interop.IO.FileOrDirectoryInfo> InNaturalOrder(this IEnumerable<Win32Interop.IO.FileOrDirectoryInfo> list) {
            var items = list.ToList();
            items.Sort((left, right) => NaturalOrder.Comparer.Compare(left.Name, right.Name));
            return items;
        }
    }
}
