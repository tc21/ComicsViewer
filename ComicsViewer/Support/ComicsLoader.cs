using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ComicsLibrary;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Uwp.Common;
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
        public static async IAsyncEnumerable<Comic> FromImportedFoldersAsync(
            UserProfile profile, IEnumerable<StorageFolder> folders, [EnumeratorCancellation] CancellationToken cc = default
        ) {
            foreach (var folder in folders) {
                await foreach (var comic in FromImportedFolderAsync(profile, folder, maxRecursionDepth: 2, cc)) {
                    yield return comic;
                }
            }
        }

        /* Used to automatically discover all comics belonging to a profile */
        public static async IAsyncEnumerable<Comic> FromProfilePathsAsync(
            UserProfile profile, [EnumeratorCancellation] CancellationToken cc = default
        ) {
            foreach (var category in profile.RootPaths) {
                await foreach (var comic in FromCategoryAsync(profile, category, cc)) {
                    yield return comic;
                }

                cc.ThrowIfCancellationRequested();
            }
        }

        /* Used to automatically update only one category */
        public static IAsyncEnumerable<Comic> FromRootPathAsync(
            UserProfile profile, NamedPath category, CancellationToken cc = default
        ) {
            if (!profile.RootPaths.Contains(category)) {
                throw new ProgrammerError();
            }

            return FromRootPathAsync(profile, category, cc);
        }

        /* Used to automatically remove comics that no longer exist. The most basic form of this function should return
         * a bool, but since this is a time-consuming task, we will do it all at once. */
        /* A proposed change is to allow choosing between checking for folder existing vs. checking for files existing */
        public static Task<List<Comic>> FindInvalidComicsAsync(
            IEnumerable<Comic> comics, CancellationToken cc, IProgress<int>? progress = null
        ) {
            return Task.Run(() => {
                var invalidComics = new List<Comic>();
                var i = 0;

                // Make a copy in case the user decides to modify the underlying list
                comics = comics.ToList();

                foreach (var comic in comics) {
                    try {
                        var comicExists = Uwp.Common.Win32Interop.IO.FileOrDirectoryExists(comic.Path);

                        if (!comicExists) {
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
            }, cc
            );
        }

        private static async IAsyncEnumerable<Comic> FromImportedFolderAsync(
            UserProfile profile, StorageFolder folder, int maxRecursionDepth, [EnumeratorCancellation] CancellationToken cc = default
        ) {
            // If in a category folder: assume it's properly laid out
            var matchingPaths = profile.RootPaths.Where(pair => folder.IsChildOf(pair.Path)).ToList();

            if (matchingPaths.Any()) {
                var bestMatch = matchingPaths.OrderBy(pair => folder.RelativeTo(pair.Path).Length).First();

                // If in a category folder: assume it's properly laid out
                var categoryName = bestMatch.Name;
                var relativePath = folder.RelativeTo(bestMatch.Path);

                // 1. Importing a category
                if (relativePath == "") {
                    await foreach (var comic in FromCategoryAsync(profile, bestMatch, cc)) {
                        yield return comic;
                    }
                    yield break;
                }

                var names = relativePath.Split(Path.DirectorySeparatorChar);
                var authorName = names[0];

                if (categoryName == UnknownCategoryName || authorName == UnknownAuthorName) {
                    yield break;
                }

                switch (names.Length) {
                    case 1:
                        // Importing an author
                        await foreach (var comic in FromAuthorFolderAsync(profile, folder, categoryName, authorName, cc)) {
                            yield return comic;
                        }
                        yield break;

                    case 2:
                        // Importing a work
                        yield return new Comic(folder.Path, folder.Name, authorName, categoryName);
                        yield break;

                    default:
                        // otherwise - we don't treat improperly laid out works as part of the category: execution falls through
                        break;
                }

            }

            // Assume we received a comic folder
            if (await profile.FolderContainsValidComicAsync(folder.Path)) {
                var names = folder.Path.Split(Path.DirectorySeparatorChar);
                var author = names.Length > 1 ? names[names.Length - 2] : UnknownAuthorName;
                yield return new Comic(folder.Path, folder.Name, author, UnknownCategoryName);
                yield break;
            }

            // Then assume it is a category/author directory
            if (maxRecursionDepth <= 0) {
                yield break;
            }

            foreach (var subfolder in await folder.GetFoldersInNaturalOrderAsync()) {
                if (UserProfile.IsIgnoredFolder(subfolder)) {
                    continue;
                }

                await foreach (var comic in FromImportedFolderAsync(profile, subfolder, maxRecursionDepth - 1, cc)) {
                    yield return comic;
                }

                cc.ThrowIfCancellationRequested();
            }
        }

        // This "magic string" is an unwritten rule: authors and categories cannot be named this.
        // TODO: eliminate the "magic" aspect.
        public const string UnknownAuthorName = "Unknown Author";
        public const string UnknownCategoryName = "Unknown Category";

        private static async IAsyncEnumerable<Comic> FromCategoryAsync(
            UserProfile profile, NamedPath rootPath, [EnumeratorCancellation] CancellationToken cc = default
        ) {
            var folder = await StorageFolder.GetFolderFromPathAsync(rootPath.Path);

            foreach (var authorFolder in await folder.GetFoldersInNaturalOrderAsync()) {
                if (UserProfile.IsIgnoredFolder(authorFolder)) {
                    continue;
                }

                await foreach (var comic in FromAuthorFolderAsync(profile, authorFolder, rootPath.Name, authorFolder.Name, cc)) {
                    yield return comic;
                }

                cc.ThrowIfCancellationRequested();
            }
        }

        private static async IAsyncEnumerable<Comic> FromAuthorFolderAsync(
            UserProfile profile, StorageFolder folder, string category, string author, [EnumeratorCancellation] CancellationToken cc = default
        ) {
            if (category == UnknownCategoryName || author == UnknownAuthorName) {
                yield break;
            }

            foreach (var comicFolder in await folder.GetFoldersInNaturalOrderAsync()) {
                if (UserProfile.IsIgnoredFolder(comicFolder)) {
                    continue;
                }

                if (await profile.FolderContainsValidComicAsync(comicFolder.Path)) {
                    yield return new Comic(comicFolder.Path, comicFolder.Name, author, category);
                }

                cc.ThrowIfCancellationRequested();
            }
        }
    }
}
