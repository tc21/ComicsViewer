using ComicsLibrary;
using ComicsViewer.Common;
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
        public static async Task OpenComicAtPathAsync(Comic comic, string path, UserProfile profile) {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);
            await OpenComicFolderAsync(comic, folder, profile);
        }

        public static async Task OpenComicFolderAsync(Comic comic, StorageFolder folder, UserProfile profile) {
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

                    var path = files.First().Path;

                    if (ImageExtensions.Contains(Path.GetExtension(path))) {
                        await LaunchBuiltinViewerAsync("d4f1d4fc-69b2-4240-9627-b2ff603e62e8_jh3a8zm8ky434", "comics-imageviewer:///path", path);
                        return;
                    }

                    if (MusicExtensions.Contains(Path.GetExtension(path))) {
                        var description = "";
                        var comicFolder = await StorageFolder.GetFolderFromPathAsync(comic.Path);
                        foreach (var descriptionSpecification in profile.ExternalDescriptions) {
                            if ((await descriptionSpecification.FetchFromFolderAsync(comicFolder)) is ExternalDescription desc) {
                                description += desc.Content;
                                description += "\n";
                            }
                        }

                        await LaunchBuiltinViewerAsync("e0dd0f61-b687-4419-81a3-3369df63b72f_jh3a8zm8ky434", "comics-musicplayer:///path", path, description);
                        return;
                    }

                    await ExpectedExceptions.IntendedBehaviorAsync(
                        title: "Cannot open item",
                        message: "The application could not open this item in a built-in viewer, " +
                            $"because it doesn't recognize its extension: '{Path.GetExtension(files.First().Name)}'.",
                        cancelled: false
                    );

                    return;

                default:
                    throw new ProgrammerError($"{nameof(OpenComicFolderAsync)}: unhandled switch case");
            }

            static async Task LaunchBuiltinViewerAsync(string packageFamilyName, string uri, string path, string? description = null) {
                var data = new ValueSet {
                    ["Path"] = path
                };

                if (description != null) {
                    data["Description"] = description;
                }

                var opt = new LauncherOptions {
                    TargetApplicationPackageFamilyName = packageFamilyName
                };

                if (!await Launcher.LaunchUriAsync(new Uri(uri), opt, data)) {
                    await ExpectedExceptions.IntendedBehaviorAsync(
                        title: "Cannot open item",
                        message: "The application failed to open this item in a built-in viewer. Is the built-in viewer installed?",
                        cancelled: false
                    );
                }
                return;
            }
        }

        public static async Task OpenContainingFolderAsync(Comic comic) {
            if (!Support.Interop.FileApiInterop.FileOrDirectoryExists(comic.Path)) {
                await ExpectedExceptions.ComicNotFoundAsync(comic);
            }

            _ = await Launcher.LaunchFolderPathAsync(comic.Path);
        }

        private static readonly string[] ImageExtensions = {
            ".bmp", ".gif", ".heic", ".heif", ".j2k", ".jfi", ".jfif", ".jif", ".jp2", ".jpe", ".jpeg", ".jpf",
            ".jpg", ".jpm", ".jpx", ".mj2", ".png", ".tif", ".tiff", ".webp"
        };

        private static readonly string[] MusicExtensions = {
            ".mp3", ".m4a", ".wav", ".flac"
        };
    }
}
