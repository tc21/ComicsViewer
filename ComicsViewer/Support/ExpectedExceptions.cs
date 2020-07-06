using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Popups;

#nullable enable

namespace ComicsViewer.Support {
    public static class ExpectedExceptions {
        public static async Task FileNotFoundAsync() {
            _ = await new MessageDialog(
                "The application attempted to open a file, but it wasn't found.", "File not found").ShowAsync();
        }

        public static async Task UnauthorizedFileSystemAccessAsync() {
            _ = await new MessageDialog(
                "Please enable file system access in settings to open comics.", "Access denied").ShowAsync();
        }

        public static async Task IntendedBehaviorAsync(string message) {
            _ = await new MessageDialog(
                message, "An operation was unsuccessful").ShowAsync();
        }

        public static async Task<bool> HandleFileRelatedExceptionsAsync(Exception e) {
            switch (e) {
                case UnauthorizedAccessException _:
                    await UnauthorizedFileSystemAccessAsync();
                    return true;
                case FileNotFoundException _:
                    await UnauthorizedFileSystemAccessAsync();
                    return true;
                default:
                    return await HandleIntendedBehaviorExceptionsAsync(e);
            }
        }

        public static async Task<bool> HandleIntendedBehaviorExceptionsAsync(Exception e) {
            switch (e) {
                case IntendedBehaviorException _:
                    await IntendedBehaviorAsync(e.Message);
                    return true;
                default:
                    return false;
            }
        }
    }
}
