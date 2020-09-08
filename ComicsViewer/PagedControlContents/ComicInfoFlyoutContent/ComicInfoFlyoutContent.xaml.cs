using ComicsViewer.Controls;
using ComicsViewer.Features;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class ComicInfoFlyoutContent : Page, IPagedControlContent {
        public ComicInfoFlyoutContent() {
            this.InitializeComponent();
        }

        private ComicInfoFlyoutViewModel? _viewModel;
        private ComicInfoFlyoutViewModel ViewModel => this._viewModel ?? throw new ProgrammerError("ViewModel must be initialized");

        private Action? EditInfoCallback { get; set; }
        public PagedControlAccessor? PagedControlAccessor { get; private set; }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            var (controller, args) = PagedControlAccessor.FromNavigationArguments<ComicInfoFlyoutNavigationArguments>(e.Parameter);
            this.PagedControlAccessor = controller;

            this.EditInfoCallback = args.EditInfoCallback;
            this._viewModel = new ComicInfoFlyoutViewModel(args.ParentViewModel, args.ComicItem);

            if (!(await this.ViewModel.LoadDescriptionsAsync(this.InfoPivotText))) {
                _ = this.Pivot.Items.Remove(this.InfoPivotItem);
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            await this.ViewModel.InitializeAsync();
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e) {
            if (!(e.ClickedItem is ComicSubitem item)) {
                throw new ProgrammerError("ListView_ItemClick received unexpected item");
            }

            await this.ViewModel.OpenItemAsync(item);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            this.PagedControlAccessor?.CloseContainer();
        }

        private void EditInfoButton_Click(object sender, RoutedEventArgs e) {
            this.PagedControlAccessor?.CloseContainer();
            this.EditInfoCallback?.Invoke();
        }
    }
}
