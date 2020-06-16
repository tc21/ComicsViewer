﻿using ComicsLibrary;
using ComicsViewer.Profiles;
using ComicsViewer.Thumbnails;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Popups;
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
    public sealed partial class EditComicInfoDialogContent : Page {
        public EditComicInfoDialogContent() {
            this.InitializeComponent();
        }

        public EditComicInfoDialogViewModel? ViewModel;
        private ContentDialog? ContainerDialog;

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is EditComicInfoDialogNavigationArguments args)
                || args.ComicItem.ItemType != ComicItemType.Work) {
                throw new ApplicationLogicException();
            }

            this.ViewModel = new EditComicInfoDialogViewModel(args.ParentViewModel, args.ComicItem);
            this.ContainerDialog = args.ContainerDialog;
        }

        private async void SaveChangesButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel!.SaveComicInfoAsync(
                title: this.ComicTitleTextBox.Text,
                author: this.ComicAuthorTextBox.Text,
                tags: this.ComicTagsTextBox.Text,
                loved: this.ComicLovedCheckBox.IsChecked ?? throw new ApplicationLogicException(),
                disliked: this.ComicDislikedCheckBox.IsChecked ?? throw new ApplicationLogicException()
            );

            this.ContainerDialog!.Hide();
        }

        private void DiscardChangesButton_Click(object sender, RoutedEventArgs e) {
            this.ContainerDialog!.Hide();
        }

        private async void EditThumbnailButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel!.ParentViewModel.TryRedefineThumbnailFromFilePickerAsync(this.ViewModel!.Item);
            this.ViewModel!.Item.DoNotifyThumbnailChanged();
        }

        private async void EditThumbnailButton_Drop(object sender, DragEventArgs e) {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) {
                return;
            }

            var items = await e.DataView.GetStorageItemsAsync();

            if (items.Count == 0) {
                return;
            }

            if (!items[0].IsOfType(StorageItemTypes.File) ||
                    !UserProfile.ImageFileExtensions.Any(ext => items[0].Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) {
                _ = await new MessageDialog("Please select an image as the new thumbnail.", "Invalid thumbnail file").ShowAsync();
                return;
            }

            await this.ViewModel!.ParentViewModel.TryRedefineThumbnailAsync(this.ViewModel!.Item, items[0].Path);
            /* Sometimes it just doesn't update properly, and I don't know why */
            this.ViewModel!.Item.DoNotifyThumbnailChanged();
        }

        private void EditThumbnailButton_DragEnter(object sender, DragEventArgs e) {
            e.AcceptedOperation =DataPackageOperation.Copy;
        }
    }
}
