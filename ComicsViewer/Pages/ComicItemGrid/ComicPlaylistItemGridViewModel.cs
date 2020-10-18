using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicPlaylistItemGridViewModel : ComicNavigationItemGridViewModel {
        private readonly List<Playlist> playlists;

        protected ComicPlaylistItemGridViewModel(MainViewModel appViewModel, List<Playlist> playlists) : base(appViewModel, ComicsInPlaylists(playlists)) {
            this.playlists = playlists;

            foreach (var playlist in this.playlists) {
                playlist.ComicsChanged += this.Comics_ComicsChanged;
            }
        }

        public static ComicPlaylistItemGridViewModel ForViewModel(MainViewModel mainViewModel, List<Playlist> playlists) {
            var viewModel = new ComicPlaylistItemGridViewModel(mainViewModel, playlists);
            // Sorts and loads the actual comic items
            viewModel.RefreshComicItems();
            return viewModel;
        }

        private static ComicView ComicsInPlaylists(IEnumerable<Playlist> playlists) {
            var comics = new ComicList();

            foreach (var playlist in playlists) {
                comics.Add(playlist.Comics);
            }

            return comics;
        }

        private protected override void SortOrderChanged() {
            var sortedPlaylists = this.GetSortedPlaylists();

            var items = sortedPlaylists.Select(playlist => new ComicNavigationItem(playlist.Name, playlist));

            this.SetComicItems(items, this.playlists.Count);
        }

        private List<Playlist> GetSortedPlaylists() {
            var copy = new List<Playlist>(this.playlists);
            copy.Sort(ComicPropertyComparers.Make(this.SelectedSortSelector));
            return copy;
        }
    }
}
