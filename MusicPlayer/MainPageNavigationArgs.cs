using Windows.Storage;

#nullable enable

namespace MusicPlayer {
    internal class MainPageNavigationArgs {
        public MainPageNavigationMode Mode { get; set; }
        public StorageFolder? Folder { get; set; }
        public StorageFile? FirstFile { get; set; }
        public string? Description { get; set; }

        private MainPageNavigationArgs() { }

        public static MainPageNavigationArgs ForFolder(StorageFolder folder, string? description = null) {
            return new MainPageNavigationArgs {
                Mode = MainPageNavigationMode.Folder,
                Folder = folder,
                Description = description
            };
        }

        public static MainPageNavigationArgs ForFirstFile(StorageFile file, string? description = null) {
            return new MainPageNavigationArgs {
                Mode = MainPageNavigationMode.FirstFile,
                FirstFile = file,
                Description = description
            };
        }
    }

    internal enum MainPageNavigationMode {
        FirstFile, Folder
    }
}
