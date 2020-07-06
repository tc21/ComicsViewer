﻿using ComicsLibrary;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Controls;
using ComicsViewer.Support;
using ComicsViewer.Support.Interop;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MoveFilesDialogContent : Page, IPagedControlContent {

        public MoveFilesDialogContent() {
            this.InitializeComponent();
        }

        public PagedControlAccessor? PagedControlAccessor { get; private set; }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            var (accessor, args) = PagedControlAccessor.FromNavigationArguments<MoveFilesDialogNavigationArguments>(e.Parameter);
            this.PagedControlAccessor = accessor;
            this.Comics = args.Comics.ToList();
            this.ParentViewModel = args.ParentViewModel;

            this.CategoryComboBox.ItemsSource = this.MainViewModel!.Profile.RootPaths;
        }

        /* We will bypass using a view model since the logic of this page is so simple */
        private List<Comic>? Comics;
        private ComicItemGridViewModel? ParentViewModel;
        private MainViewModel? MainViewModel => ParentViewModel?.MainViewModel;

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            this.PagedControlAccessor?.CloseContainer();
        }

        private void MoveComicsButton_Click(object sender, RoutedEventArgs e) {
            if (!(this.CategoryComboBox.SelectedItem is NamedPath category)) {
                throw new ApplicationLogicException();
            }

            this.MainViewModel!.StartUniqueTask(
                "moveFiles",
                $"Moving {this.Comics!.Count.PluralString("item")} to category '{category.Name}'...",
                async (cc, p) => {
                    var progress = 0;
                    // We should probably try to catch FileNotFound and UnauthorizedAccess here
                    var rootFolder = await StorageFolder.GetFolderFromPathAsync(category.Path);

                    foreach (var comic in this.Comics!) {
                        if (comic.Category != category.Name) {
                            var originalAuthorPath = Path.GetDirectoryName(comic.Path);
                            var targetPath = Path.Combine(category.Path, comic.Author, comic.Title);

                            if (FileApiInterop.FileOrDirectoryExists(targetPath)) {
                                throw new IntendedBehaviorException($"Could not move item '{comic.DisplayTitle}' " +
                                    $"because an item with the same name already exists at the destination.");
                            }

                            FileApiInterop.MoveDirectory(comic.Path, targetPath);

                            if (FileApiInterop.GetDirectoryContents(originalAuthorPath).Count() == 0) {
                                FileApiInterop.RemoveDirectory(originalAuthorPath);
                            }

                            // Although we could modify comic.Path, comic.Category, and call into database update methods,
                            // this is probably easier to maintain:
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                Windows.UI.Core.CoreDispatcherPriority.Normal,
                                async () => {
                                    comic.Path = targetPath;
                                    comic.Category = category.Name;
                                    await this.MainViewModel!.NotifyComicsChangedAsync(new[] { comic });
                                }
                            );
                        }

                        // Cancellation and progress reporting
                        if (cc.IsCancellationRequested) {
                            return;
                        }

                        p.Report(++progress);
                    }
                }, exceptionHandler: ExpectedExceptions.HandleFileRelatedExceptions
            );

            this.PagedControlAccessor?.CloseContainer();
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            this.MoveComicsButton.IsEnabled = true;
        }
    }
}