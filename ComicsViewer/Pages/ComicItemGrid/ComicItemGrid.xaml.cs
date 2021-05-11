using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Support;
using ComicsViewer.Uwp.Common;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public sealed partial class ComicItemGrid {
        private ComicItemGridViewModel? _viewModel;

        private MainViewModel MainViewModel => this.ViewModel.MainViewModel;
        public ComicItemGridViewModel ViewModel => this._viewModel ?? throw new ProgrammerError("ViewModel must be initialized");

        public ComicItemGrid() {
            this.InitializeComponent();

            // This has to be done in code, not XAML since we need the third handledEventsToo argument
            this.VisibleComicsGrid.AddHandler(
                PointerPressedEvent, new PointerEventHandler(this.VisibleComicsGrid_PointerPressed), true);
        }

        #region Clicking, double clicking, right clicking

        // This function determines if the user used their non-left-mouse-button to click, and prevents the Tapped
        // evenet from firing if that is the case.
        private void VisibleComicsGrid_PointerPressed(object sender, PointerRoutedEventArgs e) {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse) {
                var pointerPoint = e.GetCurrentPoint(this);
                if (!pointerPoint.Properties.IsLeftButtonPressed) {
                    this.isLastTapValid = false;
                    return;
                }
            }

            this.isLastTapValid = true;
        }

        private bool isLastTapValid = true;

        private void VisibleComicsGrid_Tapped(object sender, TappedRoutedEventArgs e) {
            if (!this.isLastTapValid) {
                return;
            }

            var controlKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            var shiftKeyState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift);

            if ((controlKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                (shiftKeyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down) {
                // The user is selecting something. Ignore this.
                return;
            }

            if (sender is not GridView) {
                throw new ProgrammerError("Only ComicItemGrid should be able to call this event handler");
            }

            var tappedElement = (FrameworkElement)e.OriginalSource;
            if (tappedElement.DataContext is not ComicItem item) {
                // The click happened on an empty space
                this.VisibleComicsGrid.SelectedItems.Clear();
                return;
            }

            this.PrepareNavigateIn(item);
            this.MainViewModel.NavigateInto(item);
        }
        // Prepares the grid before the right click context menu is shown
        private void VisibleComicsGrid_RightTapped(object sender, RightTappedRoutedEventArgs e) {
            if (!(sender is GridView grid)) {
                throw new ProgrammerError("Only ComicItemGrid should be able to call this event handler");
            }

            var tappedElement = (FrameworkElement)e.OriginalSource;

            if (tappedElement.DataContext is ComicItem comicItem) {
                // Selection logic that behaves like most other reasonable apps
                if (!grid.SelectedItems.Contains(comicItem)) {
                    grid.SelectedItems.Clear();
                    grid.SelectedItems.Add(comicItem);
                }
            } else {
                // The right click happened on an empty space
                this.VisibleComicsGrid.SelectedItems.Clear();
            }

            this.ComicItemGridContextFlyout.ShowAt(tappedElement, new FlyoutShowOptions {
                Position = e.GetPosition(tappedElement)
            });
        }

        #endregion

        #region Popups

        public async Task ShowEditComicInfoDialogAsync(ComicWorkItem item) {
            // Since the only preset UI is its title, there's no need to have this in the xaml. We can just create it here.
            _ = await new PagedContentDialog { Title = "Edit info" }.NavigateAndShowAsync(
                typeof(EditComicInfoDialogContent),
                new EditComicInfoDialogNavigationArguments(this.MainViewModel, item)
            );
        }

        public async Task ShowEditNavigationItemDialogAsync(ComicNavigationItem item) {
            if (!(this.ViewModel is ComicNavigationItemGridViewModel vm)) {
                throw new ProgrammerError($"{nameof(this.ShowEditNavigationItemDialogAsync)} should not be called with a work item view model");
            }

            var helper = new EditNavigationItemDialogViewModel(vm, item.Title);

            _ = await new PagedContentDialog { Title = $"{vm.NavigationTag.Describe(capitalized: true)}: {item.Title}" }.NavigateAndShowAsync(
                typeof(TextInputDialogContent),
                new TextInputDialogNavigationArguments(
                    properties: TextInputDialogProperties.ForSavingChanges("Name"),
                    initialValue: item.Title,
                    asyncAction: helper.SaveAsync,
                    validate: helper.GetItemTitleInvalidReason
                )
            );
        }

        public async Task ShowMoveFilesDialogAsync(IEnumerable<ComicItem> items) {
            /* Currently, the application is not able to handle moving files while changing its author or title. So the
             * only thing we can actually change is category. We are thus limiting the ability to move files to moving
             * between the already-defined categories. */
            var arguments = ItemPickerDialogNavigationArguments.New(
                properties: new ItemPickerDialogProperties(
                    comboBoxHeader: "Category",
                    action: "Move items",
                    actionDescription: "Move selected items to the root folder of the following category:",
                    warning: "Warning: this will move the folders containing the selected items to the root path of the chosen category. " +
                             "There may not be an easy way to undo this operation."
                ),
                items: this.MainViewModel.Profile.RootPaths,
                action: category => {
                    var comics = items.SelectMany(item => item.ContainedComics()).ToList();
                    _ = this.MainViewModel.StartMoveComicsToCategoryTaskAsync(comics, category);
                }
            );

            _ = await new PagedContentDialog { Title = "Move files to a new category" }.NavigateAndShowAsync(
                typeof(ItemPickerDialogContent),
                arguments
            );
        }

        public async Task ShowCreatePlaylistDialogAsync() {
            var arguments = new TextInputDialogNavigationArguments(
                properties: TextInputDialogProperties.ForNewItem("Playlist name"),
                initialValue: this.MainViewModel.GetProposedPlaylistName(),
                asyncAction: name => this.MainViewModel.CreatePlaylistAsync(name),
                validate: ValidatePlaylistName
            );

            _ = await new PagedContentDialog { Title = $"Create playlist" }.NavigateAndShowAsync(typeof(TextInputDialogContent), arguments);

            ValidateResult ValidatePlaylistName(string name) {
                if (this.MainViewModel.Playlists.ContainsKey(name)) {
                    return "A playlist with this name already exists.";
                }

                return ValidateResult.Ok();
            }
        }

        public async Task ShowAddItemsToPlaylistDialogAsync(IEnumerable<ComicItem> items) {
            var arguments = ItemPickerDialogNavigationArguments.New(
                properties: new ItemPickerDialogProperties(
                    comboBoxHeader: "Playlist",
                    action: "Add to playlist",
                    actionDescription: "Select a playlist to add the selected items to:",
                    warning: "Note: the same item will only be added to the same playlist once."
                ),
                items: this.MainViewModel.Playlists.Keys,
                action: async selected => await this.MainViewModel.AddToPlaylistAsync(selected, items.SelectMany(item => item.ContainedComics()))
            );

            _ = await new PagedContentDialog { Title = "Add items to a playlist " }.NavigateAndShowAsync(typeof(ItemPickerDialogContent), arguments);
        }

        #endregion

        #region Controlling from MainPage

        internal async Task ScrollToAbsoluteOffsetAsync(double offset, bool animated) {
            if (this.ViewModel.ComicItems.Count == 0) {
                return;
            }

            var scrollViewer = this.ComicsGridScrollViewer();

            // A null value means the grid hasn't even loaded yet, just ignore the request
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => 
                _ = scrollViewer?.ChangeView(null, -scrollViewer.VerticalOffset + offset, null, disableAnimation: !animated));
        }

        private ScrollViewer? ComicsGridScrollViewer() {
            // Looks like there isn't a good way to actually scroll with an animation. 
            // Both GridView.ScrollIntoView and GridView.MakeVisible scrolls instantly without animation.
            // The solution used here is a reverse-engineering that can potentially break anytime with an update to the UWP library
            if (VisualTreeHelper.GetChild(this.VisibleComicsGrid, 0) == null) {
                // this means the grid hasn't even loaded yet, just ignore the request
                return null;
            }

            if (VisualTreeHelper.GetChild(this.VisibleComicsGrid, 0) is UIElement childElement &&
                VisualTreeHelper.GetChild(childElement, 0) is ScrollViewer scrollViewer) {
                return scrollViewer;
            } else {
                throw new ProgrammerError("Failed to obtain VisibleComicsGrid's ScrollViewer child element.");
            }
        }

        private double GetScrollOffset() {
            return this.ComicsGridScrollViewer()?.VerticalOffset ?? 0;
        }

        #endregion

        #region Saving and setting state during navigation

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (e.Parameter is not ComicItemGridNavigationArguments args) {
                throw new ProgrammerError("A ComicItemGrid must receive a ComicItemGridNavigationArguments as its parameter.");
            }

            if (args.ViewModel is null) {
                throw new ProgrammerError("A ComicItemGrid must received a viewmodel in its navigation arguments");
            }

            switch (e.NavigationMode) {
                case NavigationMode.New:
                    // Initialize this page only when creating a new page, 
                    // not when the user returned to this page by pressing the back button
                    this._viewModel = args.ViewModel;
                    break;
                case NavigationMode.Back:
                case NavigationMode.Forward:
                case NavigationMode.Refresh:
                    throw new ProgrammerError("ComicItemGrid should only be navigated with NavigationMode.New");
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (args.HighlightedComicItem is { } item) {
                this.HighlightedComicItem = item;
            }

            if (this.VisibleComicsGrid.IsLoaded) {
                this.RecalculateGridItemSize(this.VisibleComicsGrid);
            }

            CoreWindow.GetForCurrentThread().ResizeStarted += this.ComicItemGrid_ResizeStarted;
            CoreWindow.GetForCurrentThread().ResizeCompleted += this.ComicItemGrid_ResizeCompleted;

            // MainPage cannot rely on ContentFrame.Navigated because we navigate to a ComicItemGridContainer, not this class
            args.OnNavigatedTo?.Invoke(this, e);
        }

        // Note: Do not override OnNavigatingFrom. ComicItemGrid must be manually destroyed.

        public ComicItem? HighlightedComicItem;

        public void FinishNavigateInConnectedAnimationIfExists(ComicItem item) {
            if (!ConnectedAnimationHelper.AnimationExists("navigateIn")) {
                return;
            }

            this.HighlightedComicItemControl.TryStartConnectedAnimationToThumbnail(item);
        }

        public void DisposeAndInvalidate() {
            // We still have to call this manually, because sometimes xaml.cs classes aren't properly thrown away and garbage collected
            this.ViewModel.DestroyComicItemsAndInvalidate();

            CoreWindow.GetForCurrentThread().ResizeStarted -= this.ComicItemGrid_ResizeStarted;
            CoreWindow.GetForCurrentThread().ResizeCompleted -= this.ComicItemGrid_ResizeCompleted;
        }

        public void PrepareNavigateIn(ComicItem item) {
            if (!item.ContainedComics().Any()) {
                return;
            }

            _ = ConnectedAnimationHelper.PrepareAnimationFromListView(this.VisibleComicsGrid, "ComicItemThumbnailContainer", item, "navigateIn");
        }

        public ComicItemGridState GetSaveState() {
            return new ComicItemGridState(this.ViewModel.ExtractComicItems(), this.GetScrollOffset(), this.MainViewModel.LastModified);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) {
            this.ViewModel.SortAndRefreshComicItems();
        }

        #endregion

        #region Redefining thumbnails

        public async Task RedefineThumbnailAsync(ComicWorkItem item) {
            if (await ExpectedExceptions.TryGetFolderWithPermission(item.Comic.Path) is not { } folder) {
                return;
            }

            _ = await new PagedContentDialog { Title = "Redefine thumbnail" }.NavigateAndShowAsync(
                typeof(RedefineThumbnailDialogContent),
                new RedefineThumbnailDialogNavigationArguments(folder.Path, item, this.MainViewModel)
            );
        }

        #endregion

        #region Dynamic resizing

        /* We will have to manage item widths manually. Note that the Windows Community Toolkit provides the ability ]
         * to do this without writing custom code using AdaptiveGridView, but that uses a min width for each item, 
         * when we want a max width for each item. */
        private bool resizing;

        private void ComicItemGrid_ResizeStarted(CoreWindow sender, object args) {
            this.resizing = true;
        }

        private void ComicItemGrid_ResizeCompleted(CoreWindow sender, object args) {
            this.resizing = false;
            this.RecalculateGridItemSize(this.VisibleComicsGrid);
        }

        private void VisibleComicsGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (!this.resizing) {
                this.RecalculateGridItemSize(this.VisibleComicsGrid);
            }
        }

        private void RecalculateGridItemSize(GridView grid) {
            var idealItemWidth = this.ViewModel.ImageWidth;
            var idealItemHeight = this.ViewModel.ImageHeight;
            var columns = Math.Ceiling(grid.ActualWidth / idealItemWidth);
            var itemsWrapGrid = (ItemsWrapGrid)grid.ItemsPanelRoot!;
            itemsWrapGrid.ItemWidth = grid.ActualWidth / columns;
            itemsWrapGrid.ItemHeight = itemsWrapGrid.ItemWidth * idealItemHeight / idealItemWidth;
        }

        #endregion

        #region Drag and drop

        private void VisibleComicsGrid_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) {
            this.DragAndDropShortcuts.Visibility = Visibility.Collapsed;
            this.DragAndDropPlaylists.ItemsSource = null;
        }

        private void VisibleComicsGrid_DragItemsStarting(object sender, DragItemsStartingEventArgs e) {
            var comicItems = e.Items.Cast<ComicItem>().ToList();
            var comicUniqueIds = comicItems.SelectMany(item => item.ContainedComics()).Select(comic => comic.UniqueIdentifier).ToArray();

            e.Data.RequestedOperation = DataPackageOperation.Copy;
            e.Data.Properties.Add("tc.comics.send_items", true);
            e.Data.Properties.Add("tc.comics.unique_ids", comicUniqueIds);
            e.Data.SetDataProvider(StandardDataFormats.StorageItems, async request => {
                var items = new List<IStorageItem>();

                // Required to call async functions
                var deferral = request.GetDeferral();

                foreach (var comicItem in comicItems) {
                    foreach (var comic in comicItem.ContainedComics()) {
                        items.Add(await StorageFolder.GetFolderFromPathAsync(comic.Path));
                    }
                }

                request.SetData(items.AsReadOnly());

                deferral.Complete();
            });

            e.Data.SetText(string.Join("\n", comicItems.SelectMany(item => item.ContainedComics()).Select(comic => comic.UniqueIdentifier)));

            var playlistItems = this.MainViewModel.Playlists.Select(
                playlist => new DragAndDropShortcutItem(
                    playlist.Name,
                    async comics => await this.MainViewModel.AddToPlaylistAsync(playlist.Name, comics)
                )
            ).ToList();

            playlistItems.Add(
                new DragAndDropShortcutItem(
                "> create new playlist...",
                    async comics => {
                        var existingPlaylists = this.MainViewModel.Playlists.Keys.ToHashSet();
                        await this.ShowCreatePlaylistDialogAsync();

                        if (this.MainViewModel.Playlists.Keys.Except(existingPlaylists).FirstOrDefault() is { } newPlaylist) {
                            await this.MainViewModel.AddToPlaylistAsync(newPlaylist, comics);
                        }
                    }
                )
            );

            this.DragAndDropShortcuts.Visibility = Visibility.Visible;
            this.DragAndDropPlaylists.ItemsSource = playlistItems;
        }

        private void VisibleComicsGrid_DragOver(object sender, DragEventArgs e) {
            if (e.Data != null && e.Data.Properties.ContainsKey("tc.comics.send_items")) {
                return;
            }

            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void VisibleComicsGrid_Drop(object sender, DragEventArgs e) {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) {
                return;
            }

            var items = await e.DataView.GetStorageItemsAsync();

            if (items.Count == 0) {
                return;
            }

            var folders = items.Where(item => item.IsOfType(StorageItemTypes.Folder))
                               .Cast<StorageFolder>();

            await this.MainViewModel.StartLoadComicsFromFoldersTaskAsync(folders);
        }

        private void DragAndDropPlaylistItem_DragOver(object sender, DragEventArgs e) {
            var element = (FrameworkElement)sender;

            if (!(element.DataContext is DragAndDropShortcutItem item)) {
                throw ProgrammerError.Auto();
            }

            if (!e.Data.Properties.ContainsKey("tc.comics.send_items") || !e.Data.Properties.ContainsKey("tc.comics.unique_ids")) {
                return;
            }

            this.DragAndDropPlaylists.SelectedItem = item;
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private void DragAndDropPlaylistItem_DragLeave(object sender, DragEventArgs e) {
            this.DragAndDropPlaylists.SelectedItem = null;
        }

        private void DragAndDropPlaylistItem_Drop(object sender, DragEventArgs e) {
            var element = (FrameworkElement)sender;

            if (!(element.DataContext is DragAndDropShortcutItem item)) {
                throw ProgrammerError.Auto();
            }

            var uniqueIds = (string[])e.Data.Properties["tc.comics.unique_ids"];
            var comics = uniqueIds.Select(id => this.MainViewModel.ComicView.GetStored(id));

            item.OnDrop(comics);
        }

        #endregion

        private async void VisibleComicsGrid_Loaded(object sender, RoutedEventArgs e) {
            this.RecalculateGridItemSize(this.VisibleComicsGrid);

            if (this.ViewModel.RequestedInitialScrollOffset is { } offset) {
                await this.ScrollToAbsoluteOffsetAsync(offset, false);
            }
        }
    }
}