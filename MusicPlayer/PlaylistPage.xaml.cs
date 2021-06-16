using System.Collections.Generic;
using ComicsViewer.Common;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace MusicPlayer {
    public sealed partial class PlaylistPage {
        private ViewModel? _parentViewModel;

        private PlaylistViewModel ViewModel { get; } = new();
        private ViewModel ParentViewModel => this._parentViewModel ?? throw ProgrammerError.Unwrapped();

        public PlaylistPage() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (e.NavigationMode is not (NavigationMode.New or NavigationMode.Refresh)) {
                return;
            }

            if (e.Parameter is not PlaylistPageNavigationArguments args) {
                throw ProgrammerError.Auto();
            }

            if (this._parentViewModel != null) {
                this._parentViewModel.PlaylistChanged -= this.ParentViewModel_PlaylistChanged;
            }

            this._parentViewModel = args.ViewModel;
            this._parentViewModel.PlaylistChanged += this.ParentViewModel_PlaylistChanged;
            this._parentViewModel.PlayStarted += this.ParentViewModel_PlayStarted;
            this.ViewModel.SetItems(args.Items);
        }

        private void ParentViewModel_PlayStarted(ViewModel sender, PlaylistItem item) {
            if (this.ListView.Items!.IndexOf(item) is var index && index != -1) { 
                this.ListView.SelectedIndex = index;
            }
        }

        private void ParentViewModel_PlaylistChanged(ViewModel sender, IReadOnlyList<PlaylistItem> items) {
            this.ViewModel.SetItems(items);
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is not PlaylistItem item) {
                // The click happened on an empty space
                this.ListView.SelectedItems.Clear();
                return;
            }

            await this.ParentViewModel.PlayAsync(item);
        }
    }

    internal class PlaylistPageNavigationArguments {
        public IEnumerable<PlaylistItem> Items { get; }
        public ViewModel ViewModel { get; }

        public PlaylistPageNavigationArguments(IEnumerable<PlaylistItem> items, ViewModel viewModel) {
            this.Items = items;
            this.ViewModel = viewModel;
        }
    }
}
