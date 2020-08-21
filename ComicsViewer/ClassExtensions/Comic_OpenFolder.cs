using ComicsLibrary;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class Comic_OpenFolder {
        /// <summary>
        /// If the folder of the comic could not be found, it just pops up a message box
        /// </summary>
        public static async Task<StorageFolder?> GetFolderAndNotifyErrorsAsync(this Comic comic) {
            try {
                return await StorageFolder.GetFolderFromPathAsync(comic.Path);
            } catch (FileNotFoundException) {
                await ExpectedExceptions.ComicNotFoundAsync(comic);
            }

            return null;
        }
    }
}
