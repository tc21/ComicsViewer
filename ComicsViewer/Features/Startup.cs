using ComicsLibrary;
using ComicsViewer.Common;
using ComicsViewer.Support;
using ComicsViewer.Uwp.Common.Win32Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;

#nullable enable

namespace ComicsViewer.Features {
    public enum StartupApplicationType {
        OpenFirstFile, BuiltinViewer
    }

    public static class Startup {
        public static async Task OpenComicSubitemAsync(ComicSubitem subitem, UserProfile profile) {
            switch (profile.StartupApplicationType) {
                case StartupApplicationType.OpenFirstFile:
                    // The if statement checks that the return value is not null
                    var file = await StorageFile.GetFileFromPathAsync(subitem.Files.First());
                    _ = await Launcher.LaunchFileAsync(file);
                    return;

                case StartupApplicationType.BuiltinViewer:
                    var files = subitem.Files.ToList();
                    var testFile = files[0];

                    if (ImageExtensions.Contains(Path.GetExtension(testFile))) {
                        await LaunchBuiltinViewerAsync("d4f1d4fc-69b2-4240-9627-b2ff603e62e8_jh3a8zm8ky434", "comics-imageviewer:///filenames", files);
                        return;
                    }

                    if (MusicExtensions.Contains(Path.GetExtension(testFile))) {
                        var description = "";
                        var comicFolder = await StorageFolder.GetFolderFromPathAsync(subitem.Comic.Path);
                        foreach (var descriptionSpecification in profile.ExternalDescriptions) {
                            if (!(await descriptionSpecification.FetchFromFolderAsync(comicFolder) is { } desc)) {
                                continue;
                            }

                            description += desc.Content;
                            description += "\n";
                        }

                        await LaunchBuiltinViewerAsync("e0dd0f61-b687-4419-81a3-3369df63b72f_jh3a8zm8ky434", "comics-musicplayer:///filenames", files, description);
                        return;
                    }

                    await ComicExpectedExceptions.IntendedBehaviorAsync(
                        title: "Cannot open item",
                        message: "The application could not open this item in a built-in viewer, " +
                            $"because it doesn't recognize its extension: '{Path.GetExtension(testFile)}'.",
                        cancelled: false
                    );

                    return;

                default:
                    throw new ProgrammerError($"{nameof(OpenComicSubitemAsync)}: unhandled switch case");
            }

            static async Task LaunchBuiltinViewerAsync(string packageFamilyName, string uri, IEnumerable<string> filenames, string? description = null) {
                var data = new ValueSet {
                    ["Filenames"] = filenames.ToArray()
                };

                if (description != null) {
                    data["Description"] = description;
                }

                var opt = new LauncherOptions {
                    TargetApplicationPackageFamilyName = packageFamilyName
                };

                if (!await Launcher.LaunchUriAsync(new Uri(uri), opt, data)) {
                    await ComicExpectedExceptions.IntendedBehaviorAsync(
                        title: "Cannot open item",
                        message: "The application failed to open this item in a built-in viewer. Is the built-in viewer installed?",
                        cancelled: false
                    );
                }
            }
        }

        public static async Task OpenContainingFolderAsync(Comic comic) {
            if (!IO.FileOrDirectoryExists(comic.Path)) {
                await ComicExpectedExceptions.ComicNotFoundAsync(comic);
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
