using ComicsViewer.Support;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicItemGridViewModelProperties {
        public NavigationTag? ParentType { get; }
        public string? PlaylistName { get; }

        public ComicItemGridViewModelProperties(NavigationTag? parentType = null, string? playlistName = null) {
            this.ParentType = parentType;
            this.PlaylistName = playlistName;
        }
    }
}