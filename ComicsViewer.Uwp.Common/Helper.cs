using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ComicsViewer.Common;
using Windows.ApplicationModel.Activation;
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
                    return new ProtocolActivatedArguments {
                        Result = ProtocolActivatedResult.InvalidArguments,
                        ErrorMessage = "Required argument 'Path' not found."
                    };
                }

                try {
                    var folder = await StorageFolder.GetFolderFromPathAsync(path);

                    return new ProtocolActivatedArguments {
                        Result = ProtocolActivatedResult.Success,
                        Mode = ProtocolActivatedMode.Folder,
                        Folder = folder
                    };
                } catch (UnauthorizedAccessException) {
                    return new ProtocolActivatedArguments {
                        Result = ProtocolActivatedResult.AccessDenied,
                        ErrorMessage = "Please ensure file system access for this app is turned on in Settings."
                    };
                } catch (FileNotFoundException) {
                    return new ProtocolActivatedArguments {
                        Result = ProtocolActivatedResult.FilesNotFound,
                        ErrorMessage = $"The directory doesn't exist: {path}"
                    };
                }
            }

            if (args.Uri.AbsolutePath == "/file") {
                throw new NotImplementedException();
            }

            if (args.Uri.AbsolutePath == "/filenames") {
                if (!(args.Data.TryGetValue("Filenames", out var o2) && o2 is string[] filenames)) {
                    return new ProtocolActivatedArguments { 
                        Result = ProtocolActivatedResult.InvalidArguments,
                        ErrorMessage = "Required argument 'Filenames' not found."
                    };
                }

                return new ProtocolActivatedArguments {
                    Result = ProtocolActivatedResult.Success,
                    Mode = ProtocolActivatedMode.Filenames,
                    Filenames = filenames
                };
            }

            return new ProtocolActivatedArguments { 
                Result = ProtocolActivatedResult.InvalidUri,
                ErrorMessage = args.Uri.AbsolutePath
            };
        }
    }

    public class ProtocolActivatedArguments {
        public ProtocolActivatedResult Result { get; internal set; }
        public ProtocolActivatedMode Mode { get; internal set; }

        public string? ErrorMessage { get; internal set; }
        public string[]? Filenames { get; internal set; }
        public StorageFolder? Folder { get; internal set; }
        public StorageFile? File { get; internal set; }
        public string? Description { get; internal set; }
    }

    public enum ProtocolActivatedResult {
        Success, InvalidUri, InvalidArguments, FilesNotFound, AccessDenied
    }

    public static class ProtocolActivatedResult_ToString {
        public static string Description (this ProtocolActivatedResult result) {
            return result switch {
                ProtocolActivatedResult.Success => "Success",
                ProtocolActivatedResult.InvalidUri => "Invalid startup URI",
                ProtocolActivatedResult.InvalidArguments => "Invalid arguments",
                ProtocolActivatedResult.FilesNotFound => "Files not found",
                ProtocolActivatedResult.AccessDenied => "Access denied",
                _ => throw new ProgrammerError("unhandled switch case")
            };
        }
    }

    public enum ProtocolActivatedMode {
        Filenames, Folder, File
    }
}
