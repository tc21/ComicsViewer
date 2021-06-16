using System.Threading.Tasks;
using ComicsLibrary;

#nullable enable

namespace ComicsViewer.Support {
    public static class ComicExpectedExceptions {
        static ComicExpectedExceptions() {
            Uwp.Common.ExpectedExceptions.ApplicationName = "Comics";
        }

        public static Task ComicNotFoundAsync(Comic comic) {
            return IntendedBehaviorAsync($"The folder for item {comic.Title} could not be found. ({comic.Path})", "Item not found");
        }

        public static Task IntendedBehaviorAsync(string message, string? title = null, bool cancelled = true)
            => Uwp.Common.ExpectedExceptions.ShowDialogAsync(message, title, cancelled);
    }
}
