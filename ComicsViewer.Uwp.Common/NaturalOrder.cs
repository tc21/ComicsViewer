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

        public static async Task<IReadOnlyList<IStorageItem>> GetItemsInNaturalOrderAsync(
            this StorageFolder item, StorageItemSortPreference sortPreference = StorageItemSortPreference.None
        ) {
            return InNaturalOrder(await item.GetItemsAsync(), sortPreference);
        }

        public static IReadOnlyList<T> InNaturalOrder<T>(
            this IEnumerable<T> list, StorageItemSortPreference sortPreference = StorageItemSortPreference.None
        ) where T : IStorageItem {
            return Sort(list, sortPreference, info => info.Name, info => info.IsOfType(StorageItemTypes.Folder));
        }

        public static IReadOnlyList<Win32Interop.IO.FileOrDirectoryInfo> InNaturalOrder(
            this IEnumerable<Win32Interop.IO.FileOrDirectoryInfo> list,
            StorageItemSortPreference sortPreference = StorageItemSortPreference.None
        ) {
            return Sort(list, sortPreference, info => info.Name,
                info => info.ItemType == Win32Interop.IO.FileOrDirectoryType.Directory);
        }

        private const int LeftFirst = -1;
        private const int RightFirst = 1;

        private static IReadOnlyList<T> Sort<T>(
            IEnumerable<T> list, StorageItemSortPreference sortPreference,
             Func<T, string> getName, Func<T, bool> isDirectory
        ) {
            var items = list.ToList();

            items.Sort((left, right) => {
                if (sortPreference == StorageItemSortPreference.None || isDirectory(left) == isDirectory(right)) {
                    return NaturalOrder.Comparer.Compare(getName(left), getName(right));
                }

                if (isDirectory(left)) {
                    return sortPreference == StorageItemSortPreference.DirectoriesFirst ? LeftFirst : RightFirst;
                } else {
                    return sortPreference == StorageItemSortPreference.DirectoriesFirst ? RightFirst : LeftFirst;
                }
            });

            return items;
        }
    }

    public enum StorageItemSortPreference {
        None, DirectoriesFirst, DirectoriesLast
    }
}
