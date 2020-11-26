using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsLibrary.Sorting;
using ComicsViewer.Common;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class ComicPlaylistItemGridViewModel : ComicNavigationItemGridViewModel {
        private Dictionary<string, Playlist> Playlists => this.MainViewModel.Playlists;

        protected ComicPlaylistItemGridViewModel(MainViewModel mainViewModel, List<Playlist> playlists) : base(mainViewModel, ComicsInPlaylists(playlists)) {
            //foreach (var playlist in this.Playlists.Values) {
            //    playlist.ComicsChanged += this.Comics_ComicsChanged;
            //}

            //mainViewModel.PlaylistChanged += this.MainViewModel_PlaylistChanged;
        }

        private void MainViewModel_PlaylistChanged(MainViewModel source, MainViewModel.PlaylistChangedArguments e) {
            //switch (e.Type) {
            //    case MainViewModel.PlaylistChangeType.Add:
            //        e.Playlist.ComicsChanged += this.Comics_ComicsChanged;
            //        break;
            //    case MainViewModel.PlaylistChangeType.Remove:
            //        e.Playlist.ComicsChanged -= this.Comics_ComicsChanged;
            //        break;
            //    default:
            //        throw ProgrammerError.Auto();
            //}

            //this.RefreshComicItems();
        }

        public static ComicPlaylistItemGridViewModel ForViewModel(MainViewModel mainViewModel, IEnumerable<Playlist> playlists) {
            var viewModel = new ComicPlaylistItemGridViewModel(mainViewModel, playlists.ToList());
            // Sorts and loads the actual comic items
            viewModel.RefreshComicItems();
            return viewModel;
        }

        private static ComicView ComicsInPlaylists(IEnumerable<Playlist> playlists) {
            var comics = new ComicList();

            foreach (var playlist in playlists) {
                comics.UnionWith(playlist);
            }

            return comics;
        }

        private protected override void SortOrderChanged() {
            var sortedPlaylists = this.GetSortedPlaylists();

            var items = sortedPlaylists.Select(playlist => new ComicNavigationItem(playlist.Name, playlist, allowEmptyItems: true));

            this.SetComicItems(items, this.Playlists.Count);
        }

        private List<Playlist> GetSortedPlaylists() {
            var copy = new List<Playlist>(this.Playlists.Values);
            copy.Sort(ComicPropertyComparers.Make(this.SelectedSortSelector));
            return copy;
        }
    }
}
