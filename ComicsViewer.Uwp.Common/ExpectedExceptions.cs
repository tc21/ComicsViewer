using System;
using System.IO;
using System.Threading.Tasks;
using ComicsViewer.Common;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer.Uwp.Common {
    public static class ExpectedExceptions {
        public static string ApplicationName { get; set; } = "The application";

        public static Task FileNotFoundAsync(string? filename = null, string? message = null, bool cancelled = true) {
            message ??= $"{ApplicationName} attempted to open a file, but it wasn't found.";

            if (filename != null) {
                message += $" ({filename})";
            }

            return ShowDialogAsync(message, "File not found", cancelled);
        }

        public static Task UnauthorizedAccessAsync(bool cancelled = true) {
            return ShowDialogAsync(
                $"{ApplicationName} could not access files that it needs to correctly work. " +
                    "Please enable file system access in settings to open comics.",
                "Access denied",
                cancelled
            );
        }

        public static async Task ShowDialogAsync(string message, string? title = null, bool cancelled = true) {
            if (cancelled) {
                message += "\n(Note: the operation that caused this error has been cancelled.)";
            }

            _ = await new ContentDialog {
                Title = title ?? "An operation was unsuccessful",
                Content = message,
                CloseButtonText = "OK"
            }.ScheduleShowAsync();
        }

        public static async Task<bool> HandleFileRelatedExceptionsAsync(Exception e) {
            switch (e) {
                case UnauthorizedAccessException _:
                    await UnauthorizedAccessAsync();
                    return true;
                case FileNotFoundException ef:
                    await FileNotFoundAsync(ef.FileName);
                    return true;
                default:
                    return await HandleIntendedBehaviorExceptionsAsync(e);
            }
        }

        public static async Task<bool> HandleIntendedBehaviorExceptionsAsync(Exception e) {
            switch (e) {
                case IntendedBehaviorException ib:
                    await ShowDialogAsync(ib.Message, ib.Title);
                    return true;
                default:
                    return false;
            }
        }

        public static async Task<StorageFile?> TryGetFileWithPermission(string path) {
            try {
                return await StorageFile.GetFileFromPathAsync(path);
            } catch (Exception e) {
                if (!await HandleFileRelatedExceptionsAsync(e)) {
                    throw;
                }

                return null;
            }
        }

        public static async Task<StorageFolder?> TryGetFolderWithPermission(string path) {
            try {
                return await StorageFolder.GetFolderFromPathAsync(path);
            } catch (Exception e) {
                if (!await HandleFileRelatedExceptionsAsync(e)) {
                    throw;
                }

                return null;
            }
        }
    }
}
