using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ComicsViewer.Common;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Uwp.Common {
    public static class Helper {
        public static async Task<ProtocolActivatedArguments> ParseActivationArguments(ProtocolActivatedEventArgs args) {
            var result = await ParseActivationArgumentsWithoutDescription(args);

            if (args.Data.TryGetValue("Description", out var o) && o is string description) {
                result.Description = description;
            }

            return result;
        }

        private static async Task<ProtocolActivatedArguments> ParseActivationArgumentsWithoutDescription(ProtocolActivatedEventArgs args) {
            if (args.Uri.AbsolutePath == "/path") {
                if (!(args.Data.TryGetValue("Path", out var o) && o is string path)) {
                    return new ProtocolErrorActivatedArguments(ProtocolActivatedErrorReason.InvalidArguments, "Required argument 'Path' not found.");
                }

                try {
                    var folder = await StorageFolder.GetFolderFromPathAsync(path);
                    return new ProtocolFoldersActivatedArguments(new[] { folder });
                } catch (UnauthorizedAccessException) {
                    return new ProtocolErrorActivatedArguments(
                        ProtocolActivatedErrorReason.AccessDenied,
                        "Please ensure file system access for this app is turned on in Settings.");
                } catch (FileNotFoundException) {
                    return new ProtocolErrorActivatedArguments(ProtocolActivatedErrorReason.FilesNotFound, $"The directory doesn't exist: {path}");
                }
            }

            if (args.Uri.AbsolutePath == "/file") {
                throw new NotImplementedException();
            }

            if (args.Uri.AbsolutePath == "/filenames") {
                if (!(args.Data.TryGetValue("Filenames", out var o2) && o2 is string[] filenames)) {
                    return new ProtocolErrorActivatedArguments(ProtocolActivatedErrorReason.InvalidArguments, "Required argument 'Filenames' not found.");
                }

                return new ProtocolFilenamesActivatedArguments(filenames);
            }

            if (args.Uri.AbsolutePath == "/shared_filelist") {
                if (!(args.Data.TryGetValue("FileToken", out var o) && o is string token)) {
                    return new ProtocolErrorActivatedArguments(ProtocolActivatedErrorReason.InvalidArguments, "Required argument 'FileToken' not found.");
                }

                StorageFile file;

                try {
                    file = await SharedStorageAccessManager.RedeemTokenForFileAsync(token);
                } catch {
                    return new ProtocolErrorActivatedArguments(
                        ProtocolActivatedErrorReason.InvalidArguments,
                        $"Could not retrieve file corresponding to file token '{token}'.");
                }

                var lines = await FileIO.ReadLinesAsync(file);

                // the temp file is no longer needed
                await file.DeleteAsync();

                return new ProtocolFilenamesActivatedArguments(lines);
            }

            return new ProtocolErrorActivatedArguments(ProtocolActivatedErrorReason.InvalidUri, args.Uri.AbsolutePath);
        }
    }

    public abstract class ProtocolActivatedArguments {
        public string? Description { get; set; }

        private protected ProtocolActivatedArguments() { }
    }

    public sealed class ProtocolFilenamesActivatedArguments : ProtocolActivatedArguments {
        public IEnumerable<string> Filenames { get; }

        public ProtocolFilenamesActivatedArguments(IEnumerable<string> filenames) {
            this.Filenames = filenames;
        }
    }

    public sealed class ProtocolFilesActivatedArguments : ProtocolActivatedArguments {
        public IEnumerable<StorageFile> Files { get; }

        public ProtocolFilesActivatedArguments(IEnumerable<StorageFile> files) {
            this.Files = files;
        }
    }

    public sealed class ProtocolFoldersActivatedArguments : ProtocolActivatedArguments {
        public IEnumerable<StorageFolder> Folders { get; }

        public ProtocolFoldersActivatedArguments(IEnumerable<StorageFolder> folders) {
            this.Folders = folders;
        }
    }

    public sealed class ProtocolContainingFileActivatedArguments : ProtocolActivatedArguments {
        public StorageFile File { get; }

        public ProtocolContainingFileActivatedArguments(StorageFile file) {
            this.File = file;
        }
    }

    public sealed class ProtocolErrorActivatedArguments : ProtocolActivatedArguments {
        public ProtocolActivatedErrorReason Reason { get; }
        public string ErrorMesage { get; }

        public ProtocolErrorActivatedArguments(ProtocolActivatedErrorReason reason, string errorMessage) {
            this.Reason = reason;
            this.ErrorMesage = errorMessage;
        }
    }

    public enum ProtocolActivatedErrorReason {
        InvalidUri, InvalidArguments, FilesNotFound, AccessDenied
    }

    public static class ProtocolActivatedResult_ToString {
        public static string Description(this ProtocolActivatedErrorReason reason) {
            return reason switch {
                ProtocolActivatedErrorReason.InvalidUri => "Invalid startup URI",
                ProtocolActivatedErrorReason.InvalidArguments => "Invalid arguments",
                ProtocolActivatedErrorReason.FilesNotFound => "Files not found",
                ProtocolActivatedErrorReason.AccessDenied => "Access denied",
                _ => throw new ProgrammerError("unhandled switch case")
            };
        }
    }

    public enum ProtocolActivatedMode {
        Filenames, Files, Folders, ContainingFile
    }
}
