using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ComicsLibrary;
using ComicsViewer.Common;
using ComicsViewer.Support;
using ComicsViewer.Uwp.Common.Win32Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;

#nullable enable

namespace ComicsViewer.Features {
    // TODO: build a settings option to choose this
    public enum StartupApplicationType {
        OpenFirstFile, BuiltinViewer, OpenContainingFolder
    }

    public static class Startup {
        public static async Task OpenComicSubitemAsync(ComicSubitem subitem, UserProfile profile) {
            switch (profile.StartupApplicationType) {
                case StartupApplicationType.OpenFirstFile:
                    // The if statement checks that the return value is not null
                    var file = await StorageFile.GetFileFromPathAsync(subitem.Files.First());
                    _ = await Launcher.LaunchFileAsync(file);
                    return;

                case StartupApplicationType.OpenContainingFolder:
                    _ = await Launcher.LaunchFolderPathAsync(subitem.RootPath);
                    return;

                case StartupApplicationType.BuiltinViewer:
                    var files = subitem.Files.ToList();
                    var testFile = files[0];

                    if (FileTypes.IsImage(testFile)) {
                        // apparently if we transfer too much information in memory we cause an error. (The size of ValueSet is capped at ~100kb).
                        if (files.Count > 250) {
                            var tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("comics_file_list.txt", CreationCollisionOption.ReplaceExisting);
                            await FileIO.WriteLinesAsync(tempFile, files);
                            var fileToken = SharedStorageAccessManager.AddFile(tempFile);

                            await LaunchBuiltInViewerWithFileTokenAsync("d4f1d4fc-69b2-4240-9627-b2ff603e62e8_jh3a8zm8ky434", "comics-imageviewer:///shared_filelist", fileToken);
                        } else {
                            await LaunchBuiltinViewerWithFilenamesAsync("d4f1d4fc-69b2-4240-9627-b2ff603e62e8_jh3a8zm8ky434", "comics-imageviewer:///filenames", files);
                        }

                        return;
                    }

                    if (FileTypes.IsMusic(testFile)) {
                        var description = "";
                        var comicFolder = await StorageFolder.GetFolderFromPathAsync(subitem.Comic.Path);
                        foreach (var descriptionSpecification in profile.ExternalDescriptions) {
                            if (await descriptionSpecification.FetchFromFolderAsync(comicFolder) is not { } desc) {
                                continue;
                            }

                            description += desc.Content;
                            description += "\n";
                        }

                        await LaunchBuiltinViewerWithFilenamesAsync("e0dd0f61-b687-4419-81a3-3369df63b72f_jh3a8zm8ky434", "comics-musicplayer:///filenames", files, description);
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

            static Task LaunchBuiltinViewerWithFilenamesAsync(string packageFamilyName, string uri, IEnumerable<string> filenames, string? description = null) {
                return FinishLaunchingBuiltinViewerAsync(new() { ["Filenames"] = filenames.ToArray() }, packageFamilyName, uri, description);
            }

            static Task LaunchBuiltInViewerWithFileTokenAsync(string packageFamilyName, string uri, string token, string? description = null) {
                return FinishLaunchingBuiltinViewerAsync(new() { ["FileToken"] = token }, packageFamilyName, uri, description);
            }

            static async Task FinishLaunchingBuiltinViewerAsync(ValueSet data, string packageFamilyName, string uri, string? description) {
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
    }
}
