using ComicsLibrary;
using System;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Support {
    public static class ExpectedExceptions {
        static ExpectedExceptions() {
            Uwp.Common.ExpectedExceptions.ApplicationName = "Comics";
        }

        public static Task ComicNotFoundAsync(Comic comic) {
            return IntendedBehaviorAsync($"The folder for item {comic.Title} could not be found. ({comic.Path})", "Item not found");
        }
        public static Task FileNotFoundAsync(string? filename = null, string? message = null, bool cancelled = true)
            => Uwp.Common.ExpectedExceptions.FileNotFoundAsync(filename, message, cancelled);

        public static Task UnauthorizedAccessAsync(bool cancelled = true) => Uwp.Common.ExpectedExceptions.UnauthorizedAccessAsync(cancelled);

        public static Task IntendedBehaviorAsync(string message, string? title = null, bool cancelled = true)
            => Uwp.Common.ExpectedExceptions.ShowDialogAsync(message, title, cancelled);

        public static Task<bool> HandleFileRelatedExceptionsAsync(Exception e) => Uwp.Common.ExpectedExceptions.HandleFileRelatedExceptionsAsync(e);

        public static Task<bool> HandleIntendedBehaviorExceptionsAsync(Exception e) => Uwp.Common.ExpectedExceptions.HandleIntendedBehaviorExceptionsAsync(e);
    }
}
