using ComicsLibrary;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer.Features {
    public enum StartupApplicationType {
        OpenFirstFile, BuiltinViewer
    }

    public static class Startup {
        public static async Task OpenComicAtPathAsync(string path, UserProfile profile) {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);
            await OpenComicFolderAsync(folder, profile);
        }

        public static async Task OpenComicFolderAsync(StorageFolder folder, UserProfile profile) {
            switch (profile.StartupApplicationType) {
                case StartupApplicationType.OpenFirstFile:
                    // The if statement checks that the return value is not null
                    if (await profile.GetFirstFileForComicAsync(folder) is StorageFile file) {
                        // There's no reason for us to wait for the file to actually launch
                        _ = await Launcher.LaunchFileAsync(file);
                    }
                    return;

                case StartupApplicationType.BuiltinViewer:
                    var files = await profile.GetTopLevelFilesForFolderAsync(folder);

                    if (!files.Any()) {
                        return;
                    }

                    var firstFile = files.First();

                    if (ImageExtensions.Contains(Path.GetExtension(firstFile.Name))) {
                        var data = new ValueSet {
                            ["FirstFileToken"] = SharedStorageAccessManager.AddFile(firstFile)
                        };

                        var opt = new LauncherOptions {
                            TargetApplicationPackageFamilyName = "d4f1d4fc-69b2-4240-9627-b2ff603e62e8_jh3a8zm8ky434"
                        };

                        if (!await Launcher.LaunchUriAsync(new Uri("comics-imageviewer:///files"), opt, data)) {
                            await ExpectedExceptions.IntendedBehaviorAsync(
                                title: "Cannot open item",
                                message: "The application failed to open this item in a built-in viewer. Is the built-in viewer installed?",
                                cancelled: false
                            );
                        }
                        return;
                    }

                    await ExpectedExceptions.IntendedBehaviorAsync(
                        title: "Cannot open item",
                        message: "The application could not open this item in a built-in viewer, " +
                            $"because it doesn't recognize its extension: '{Path.GetExtension(firstFile.Name)}'.",
                        cancelled: false
                    );

                    return;

                default:
                    throw new ProgrammerError($"{nameof(OpenComicFolderAsync)}: unhandled switch case");
            }
        }

        public static async Task OpenContainingFolderAsync(Comic comic) {
            if (!Support.Interop.FileApiInterop.FileOrDirectoryExists(comic.Path)) {
                await Support.ExpectedExceptions.ComicNotFoundAsync(comic);
            }

            _ = await Launcher.LaunchFolderPathAsync(comic.Path);
        }

        private static readonly string[] ImageExtensions = {
            ".bmp", ".gif", ".heic", ".heif", ".j2k", ".jfi", ".jfif", ".jif", ".jp2", ".jpe", ".jpeg", ".jpf",
            ".jpg", ".jpm", ".jpx", ".mj2", ".png", ".tif", ".tiff", ".webp"
        }; 
    }
}
