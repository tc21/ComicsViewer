using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComicsLibrary;
using ComicsViewer.Uwp.Common;
using ComicsViewer.Uwp.Common.Win32Interop;

#nullable enable

namespace ComicsViewer.Features {
    public class ComicSubitem {
        public Comic Comic { get; }
        public string RootPath { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> Files { get; }

        public ComicSubitem(Comic comic, string displayName, string root, IEnumerable<string> files) {
            this.Comic = comic;
            this.RootPath = root;
            this.DisplayName = displayName;
            this.Files = files.ToList();
        }

        public async Task<bool> VerifyExistsOnDiskAsync(bool useDefaultPrompt = true) {
            var exists = IO.FileOrDirectoryExists(this.Files.First());

            if (useDefaultPrompt && !exists) {
                await ExpectedExceptions.FileNotFoundAsync(this.RootPath, "This subitem was not found on disk.");
            }

            return exists;
        }
    }
}