using ComicsLibrary;
using ComicsViewer.Common;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer.Support {
    public static class ExpectedExceptions {
        public static Task FileNotFoundAsync(string? filename = null, string message = "The application attempted to open a file, but it wasn't found.", bool cancelled = true) {
            if (filename != null) {
                message += $" ({filename})";
            }

            return IntendedBehaviorAsync(message, "File not found", cancelled);
        }
        public static Task ComicNotFoundAsync(Comic comic) {
            return IntendedBehaviorAsync($"The folder for item {comic.Title} could not be found. ({comic.Path})", "Item not found");
        }

        public static Task UnauthorizedAccessAsync(bool cancelled = true) {
            return IntendedBehaviorAsync(
                "Comics could not access files that it needs to correctly work. " +
                    "Please enable file system access in settings to open comics.", 
                "Access denied", 
                cancelled
            );
        }

        public static async Task IntendedBehaviorAsync(string message, string title = "An operation was unsuccessful", bool cancelled = true) {
            if (cancelled) {
                message += "\n(Note: the operation that caused this error has been cancelled.)";
            }

            _ = await new ContentDialog {
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            }.ShowAsync();
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
                    await IntendedBehaviorAsync(ib.Message, ib.Title);
                    return true;
                default:
                    return false;
            }
        }
    }
}
