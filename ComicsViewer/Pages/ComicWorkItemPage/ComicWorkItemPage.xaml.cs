using System;
using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public sealed partial class ComicWorkItemPage : Page, IMainPageContent {
        public ComicWorkItemPage() {
            this.InitializeComponent();
        }

        private ComicWorkItemPageViewModel? _viewModel;
        private ComicWorkItemPageViewModel ViewModel => this._viewModel ?? throw ProgrammerError.Unwrapped();
        private MainViewModel MainViewModel => this.ViewModel.MainViewModel;

        public NavigationTag NavigationTag { get; }
        public NavigationPageType NavigationPageType => NavigationPageType.WorkItem;
        public Page Page => this;
        public ComicItemGrid? ComicItemGrid { get; }

        public event Action<IMainPageContent>? Initialized;

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is not ComicWorkItemPageNavigationArguments args) {
                throw new ProgrammerError("A ComicWorkItemPage must receive a ComicWorkItemPageNavigationArguments as its parameter.");
            }

            if (e.NavigationMode is NavigationMode.Back) {
                _ = ComicItemGridCache.PopStack(this.NavigationTag, this.ViewModel.ComicItem.Title);
            }

            this._viewModel = args.ViewModel;

            _ = this.HighlightedComicItem.TryStartConnectedAnimationToThumbnail(this.ViewModel.ComicItem);

            this.Initialized?.Invoke(this);
            await this.ViewModel.InitializeAsync();

            this.SynchronizeLovedStatusToUI();

            if (await this.ViewModel.TryLoadDescriptionsAsync(this.InfoTextBlock)) {
                this.ToggleInfoPaneButton.Visibility = Visibility.Visible;
            }

            await this.ViewModel.LoadThumbnailsAsync();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
            // For now, we push useless information onto the cache to keep everything else working
            if (e.NavigationMode is NavigationMode.New) {
                ComicItemGridCache.PushStack(this.NavigationTag, this.ViewModel.ComicItem.Title, new ComicItemGridState(new(), 0));
            }
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel.OpenComicItemAsync();
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e) {
            // TODO this code is copied from ComicItemGrid. We should evaluate whether it needs to be generalized.
            _ = await new PagedContentDialog { Title = "Edit info" }.NavigateAndShowAsync(
                typeof(EditComicInfoDialogContent),
                new EditComicInfoDialogNavigationArguments(this.MainViewModel, this.ViewModel.ComicItem)
            );
        }

        private async void LoveButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel.ToggleLovedStatus();
            this.SynchronizeLovedStatusToUI();
        }

        private async void ShowInExplorerFlyoutItem_Click(object sender, RoutedEventArgs e) {
            await Startup.OpenContainingFolderAsync(this.ViewModel.ComicItem.Comic);
        }

        private void ShowAuthorFlyoutItem_Click(object sender, RoutedEventArgs e) {
            // This animation has such a low chance of succeeding, that I feel I should just remove it...
            _ = this.HighlightedComicItem.PrepareConnectedAnimationFromThumbnail(this.ViewModel.ComicItem);
            this.MainViewModel.NavigateToAuthor(this.ViewModel.ComicItem.Comic.Author);
        }

        private void SynchronizeLovedStatusToUI() {
            var loved = this.ViewModel.ComicItem.IsLoved;

            if (loved) {
                _ = VisualStateManager.GoToState(this, "Loved", false);
                this.LoveButton.IsChecked = true;
            } else {
                _ = VisualStateManager.GoToState(this, "NotLoved", false);
                this.LoveButton.IsChecked = false;
            }
        }

        private void ToggleInfoPaneButton_Checked(object sender, RoutedEventArgs e) {
            _ = VisualStateManager.GoToState(this, "InfoVisible", true);
        }

        private void ToggleInfoPaneButton_Unchecked(object sender, RoutedEventArgs e) {
            _ = VisualStateManager.GoToState(this, "InfoHidden", true);
        }

        private async void ComicSubitemGrid_ItemClick(object sender, ItemClickEventArgs e) {
            if (e.ClickedItem is not ComicSubitemContainer container) {
                throw new ProgrammerError("ComicSubitemGrid should only contain ComicSubitemContainer objects.");
            }

            await this.ViewModel.OpenComicItemAsync(container.Subitem);
        }

        private void ComicSubitemGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            this.RecalculateGridItemSize(this.ComicSubitemGrid);
        }

        private void RecalculateGridItemSize(GridView grid) {
            // TODO this code is also copied from ComicItemGrid. We should evaluate whether it needs to be generalized.
            var idealItemWidth = this.ViewModel.ImageWidth;
            var idealItemHeight = this.ViewModel.ImageHeight;
            var columns = Math.Ceiling(grid.ActualWidth / idealItemWidth);
            var itemsWrapGrid = (ItemsWrapGrid)grid.ItemsPanelRoot!;
            itemsWrapGrid.ItemWidth = grid.ActualWidth / columns;
            itemsWrapGrid.ItemHeight = itemsWrapGrid.ItemWidth * idealItemHeight / idealItemWidth;
        }

        private void ComicSubitemGrid_Loaded(object sender, RoutedEventArgs e) {
            this.RecalculateGridItemSize(this.ComicSubitemGrid);

            // TODO: we should probably implement scrolling to saved offsets.
        }
    }
}
