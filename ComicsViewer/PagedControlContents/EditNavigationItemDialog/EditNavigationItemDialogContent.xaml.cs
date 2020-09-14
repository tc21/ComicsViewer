using ComicsViewer.Common;
using ComicsViewer.Controls;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

namespace ComicsViewer.Pages {
    public sealed partial class EditNavigationItemDialogContent : Page, IPagedControlContent {
        /* Note: this class is currently only used to rename tags. It will be expanded in the future. */
        public EditNavigationItemDialogContent() {
            this.InitializeComponent();
        }


        private EditNavigationItemDialogViewModel? _viewModel;
        private EditNavigationItemDialogViewModel ViewModel => this._viewModel ?? throw new ProgrammerError("ViewModel must be initialized");

        public PagedControlAccessor? PagedControlAccessor { get; private set; }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            var (controller, args) =
                PagedControlAccessor.FromNavigationArguments<EditNavigationItemDialogNavigationArguments>(e.Parameter);
            this.PagedControlAccessor = controller;

            this._viewModel = new EditNavigationItemDialogViewModel(args.ParentViewModel, args.PropertyName);

            this.EditItemTitleTextBox.RegisterHandlers(
                get: () => this.ViewModel.ItemTitle,
                saveAsync: async value => {
                    await this.ViewModel.SaveAsync(value);
                    this.PagedControlAccessor.CloseContainer();
                },
                validate: value => this.ViewModel.GetItemTitleInvalidReason(value)
            );
        }

        private void DiscardChangesButton_Click(object sender, RoutedEventArgs e) {
            this.PagedControlAccessor!.CloseContainer();
        }
    }
}
