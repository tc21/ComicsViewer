using ComicsLibrary;
using ComicsViewer.Profiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Support {
    /* As much as I want to move this class into ComicsLibrary, it's very integrated with File I/O, and UWP, WPF, etc.
     * uses different methods to do File I/O (StorageFile vs FileInfo) */
    public static class ComicsLoader {
        /* Used when the user manually imports some folders. Automatically detects category/author according to the predefined structure. */
        public static async Task<IEnumerable<Comic>> FromImportedFolderAsync(
            UserProfile profile, StorageFolder folder, CancellationToken cc, IProgress<int>? progress = null
        ) {
            var result = new List<Comic>();
            await FinishFromImportedFolderAsync(result, profile, folder, cc, progress, 2);
            return result;
        }

        /* Used to automatically discover all comics belonging to a profile */
        public static async Task<IEnumerable<Comic>> FromProfilePathsAsync(
            UserProfile profile, CancellationToken cc, IProgress<int>? progress = null) {
            var result = new List<Comic>();

            foreach (var category in profile.RootPaths) {
                await FinishFromRootPathAsync(result, profile, category, cc, progress);

                if (cc.IsCancellationRequested) {
                    return result;
                }
            }

            return result;
        }

        /* Used to automatically update only one category */
        public async static Task<IEnumerable<Comic>> FromRootPathAsync(
            UserProfile profile, NamedPath category, CancellationToken cc, IProgress<int>? progress = null
        ) {
            if (!profile.RootPaths.Contains(category)) {
                throw new ApplicationLogicException();
            }

            var result = new List<Comic>();
            await FinishFromRootPathAsync(result, profile, category, cc, progress);
            return result;
        }

        private static async Task FinishFromImportedFolderAsync(
            List<Comic> comics, UserProfile profile, StorageFolder folder, CancellationToken cc, IProgress<int>? progress, int maxRecursionDepth) {

            // First assume the work is a comic
            if ((await profile.GetFilesForComicFolderAsync(folder)).Count() > 0) {
                var names = folder.Path.Split(Path.DirectorySeparatorChar);
                var author = names.Length > 1 ? names[names.Length - 2] : "Unknown Author";
                var category = "Unknown Category";

                // Determine category
                foreach (var pair in profile.RootPaths) {
                    if (IsChildOf(pair.Path, folder.Path)) {
                        category = pair.Name;
                        break;
                    }
                }

                var comic = new Comic(folder.Path, folder.Name, author, category);
                comics.Add(comic);
                progress?.Report(comics.Count);
                return;
            }

            // Then assume it is a category/author directory
            if (maxRecursionDepth <= 0) {
                return;
            }

            foreach (var subfolder in await folder.GetFoldersAsync()) {
                await FinishFromImportedFolderAsync(comics, profile, subfolder, cc, progress, maxRecursionDepth - 1);

                if (cc.IsCancellationRequested) {
                    return;
                }
            }

            return;
        }

        private static async Task FinishFromRootPathAsync(
            List<Comic> comics, UserProfile profile, NamedPath rootPath, CancellationToken cc, IProgress<int>? progress
        ) {
            var folder = await StorageFolder.GetFolderFromPathAsync(rootPath.Path);

            foreach (var authorFolder in await folder.GetFoldersAsync()) {
                await FinishFromAuthorFolderAsync(comics, profile, authorFolder, rootPath.Name, authorFolder.Name, cc, progress);

                if (cc.IsCancellationRequested) {
                    return;
                }
            }

            return;
        }

        private static async Task FinishFromAuthorFolderAsync(
            List<Comic> comics, UserProfile profile, StorageFolder folder, string category, string author, 
            CancellationToken cc, IProgress<int>? progress
        ) {
            foreach (var comicFolder in await folder.GetFoldersAsync()) {
                if ((await profile.GetFilesForComicFolderAsync(comicFolder)).Count() > 0) {
                    var comic = new Comic(comicFolder.Path, comicFolder.Name, author, category);
                    comics.Add(comic);
                    progress?.Report(comics.Count);
                }

                if (cc.IsCancellationRequested) {
                    return;
                }
            }

            return;
        }

        // We trust that the we pass in consistently formatted names
        private static bool IsChildOf(string parent, string child) {
            if (!Path.IsPathRooted(parent) || !Path.IsPathRooted(child)) {
                throw new ApplicationLogicException();
            }

            if (parent.Length < child.Length) {
                child = Path.GetDirectoryName(child);
                if (parent.Equals(child, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }
    }
}
