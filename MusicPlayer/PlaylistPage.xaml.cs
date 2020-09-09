using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using ComicsViewer.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace MusicPlayer {
    public sealed partial class PlaylistPage : Page {
        private ViewModel? _parentViewModel;

        private readonly PlaylistViewModel ViewModel = new PlaylistViewModel();
        private ViewModel ParentViewModel => this._parentViewModel ?? throw ProgrammerError.Auto();

        public PlaylistPage() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (e.NavigationMode == NavigationMode.New || e.NavigationMode == NavigationMode.Refresh) {
                if (!(e.Parameter is PlaylistPageNavigationArguments args)) {
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
        }

        private void ParentViewModel_PlayStarted(ViewModel sender, PlaylistItem item) {
            if (this.ListView.Items.IndexOf(item) is var index && index != -1) { 
                this.ListView.SelectedIndex = index;
            }
        }

        private void ParentViewModel_PlaylistChanged(ViewModel sender, IReadOnlyList<PlaylistItem> items) {
            this.ViewModel.SetItems(items);
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e) {
            if (!(e.ClickedItem is PlaylistItem item)) {
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
