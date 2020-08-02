using ComicsLibrary;
using ComicsViewer.Features;
using ComicsViewer.ClassExtensions;
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
        /* A summary of how file searching works:
         * The user provides a folder, or a list of folders. The directory structure must be:
         *  <category folder>\<author folder>\<work folder>\<work files>
         * 
         * For FromProfilePaths, the user configures a Mapping : <category display name> -> <category folder>
         * For FromRootPath, the user manually passes in a NamedPath { Name = <category display name>, Path = <category folder> }
         * For FromImportedFolders, the supplied folder can be the category, author, or work folder. The program simply
         *     assumes that the first folder it finds that contains work files is the work folder.
         *     
         * Folders starting with an ignored prefix will be ignored. However exceptions are made when the user explicitly
         *     requests a folder with an ignored prefix, either by naming it as a category or dropping it in.
         */

        /* Used when the user manually imports some folders. Automatically detects category/author according to the predefined structure. */
        public static async Task<IEnumerable<Comic>> FromImportedFoldersAsync(
            UserProfile profile, IEnumerable<StorageFolder> folders, CancellationToken cc, IProgress<int>? progress = null
        ) {
            var result = new List<Comic>();

            foreach (var folder in folders) {
                await FinishFromImportedFolderAsync(result, profile, folder, cc, progress, 2);
            }

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

        /* Used to automatically remove comics that no longer exist. The most basic form of this function should return
         * a bool, but since this is a time-consuming task, we will do it all at once. */
        /* A proposed change is to allow choosing between checking for folder existing vs. checking for files existing */
        public async static Task<IEnumerable<Comic>> FindInvalidComics(
            IEnumerable<Comic> comics, UserProfile profile, bool checkFiles, CancellationToken cc, IProgress<int>? progress = null
        ) {
            var invalidComics = new List<Comic>();
            var i = 0;

            // Make a copy in case the user decides to modify the underlying list
            comics = comics.ToList();

            foreach (var comic in comics) {
                try {
                    var folder = await StorageFolder.GetFolderFromPathAsync(comic.Path);

                    if (checkFiles && (await profile.FolderContainsValidComicAsync(folder))) {
                        invalidComics.Add(comic);
                    }

                } catch (FileNotFoundException) {
                    invalidComics.Add(comic);
                }

                i += 1;
                progress?.Report(i);
                if (cc.IsCancellationRequested) {
                    return invalidComics;
                }
            }

            return invalidComics;
        }

        private static async Task FinishFromImportedFolderAsync(
            List<Comic> comics, UserProfile profile, StorageFolder folder, CancellationToken cc, IProgress<int>? progress, int maxRecursionDepth) {

            // First assume the work is a comic
            if (await profile.FolderContainsValidComicAsync(folder)) {
                var names = folder.Path.Split(Path.DirectorySeparatorChar);
                var author = names.Length > 1 ? names[names.Length - 2] : "Unknown Author";
                var category = "Unknown Category";

                // Determine category
                foreach (var pair in profile.RootPaths) {
                    if (folder.Path.IsChildOfDirectory(pair.Path)) {
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
                if (UserProfile.IgnoredFilenamePrefixes.Any(prefix => subfolder.Name.StartsWith(prefix))) {
                    continue;
                }

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
                if (UserProfile.IgnoredFilenamePrefixes.Any(prefix => authorFolder.Name.StartsWith(prefix))) {
                    continue;
                }

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
                if (UserProfile.IgnoredFilenamePrefixes.Any(prefix => comicFolder.Name.StartsWith(prefix))) {
                    continue;
                }

                if (await profile.FolderContainsValidComicAsync(comicFolder)) {
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
    }
}
