using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage {
        public SettingsPage() {
            this.InitializeComponent();
        }

        private SettingsPageViewModel? _viewModel;
        public SettingsPageViewModel ViewModel => this._viewModel ?? throw new ProgrammerError("ViewModel must be initialized");
        private MainViewModel MainViewModel => this.ViewModel.MainViewModel;

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            if (!(e.Parameter is SettingsPageNavigationArguments args)) {
                throw new ProgrammerError();
            }

            this._viewModel = new SettingsPageViewModel(args.MainViewModel, args.Profile);
        }

        #region Creating a new profile 

        private async void NewProfileButton_Click(object sender, RoutedEventArgs e) {
            var result = await this.NewProfileDialog.ShowAsync();
            if (result != ContentDialogResult.Primary) {
                return;
            }

            await this.ViewModel.CreateProfileAsync(this.NewProfileTextBox.Text,
                copyCurrent: this.NewProfileRadioButtons.SelectedItem == this.NewProfileCopyCurrentProfileRadioButton);
        }

        private void NewProfileTypeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            this.UpdateNewProfileWarningText();
        }

        private void NewProfileTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            this.UpdateNewProfileWarningText();
        }

        private void UpdateNewProfileWarningText() {
            if (this.NewProfileRadioButtons.SelectedIndex == -1) {
                // the default warning should still be shown
                return;
            }

            if (Path.GetInvalidFileNameChars().Any(c => this.NewProfileTextBox.Text.Contains(c))) {
                this.NewProfileWarningTextBlock.Text = "The profile name contains invalid characters.";
                this.NewProfileDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            this.NewProfileDialog.IsPrimaryButtonEnabled = true;
            this.NewProfileWarningTextBlock.Text = "";
        }

        #endregion

        private void AddProfileCategoryButton_Click(object sender, RoutedEventArgs e) {
            this.ViewModel.AddEmptyProfileCategory();
        }

        private void ProfileCategoryDataGrid_CellEditEnded(object sender, Microsoft.Toolkit.Uwp.UI.Controls.DataGridCellEditEndedEventArgs e) {
            this.SaveProfleCategoriesButton.Visibility = Visibility.Visible;
        }

        private async void SaveProfileCategoriesButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel.SaveProfileCategoriesAsync();
            this.SaveProfleCategoriesButton.Visibility = Visibility.Collapsed;
        }

        private async void ReloadAllCategoriesButton_Click(object sender, RoutedEventArgs e) {
            var result = await this.ReloadConfirmationContentDialog.ShowAsync();

            if (result != ContentDialogResult.Primary) {
                return;
            }

            await this.MainViewModel.StartReloadAllComicsTaskAsync();
        }

        private async void ReloadCategoryButton_Click(object sender, RoutedEventArgs e) {
            var result = await this.ReloadConfirmationContentDialog.ShowAsync();

            if (result != ContentDialogResult.Primary) {
                return;
            }

            if (!(((FrameworkElement)sender).DataContext is NamedPath namedPath)) {
                throw new ProgrammerError();
            }

            await this.MainViewModel.StartReloadCategoryTaskAsync(namedPath);
        }


        private void ProfileDescriptionsDataGrid_CellEditEnded(object sender, Microsoft.Toolkit.Uwp.UI.Controls.DataGridCellEditEndedEventArgs e) {
            this.SaveProfleDescriptionsButton.Visibility = Visibility.Visible;
        }

        private void AddProfileDescriptionsButton_Click(object sender, RoutedEventArgs e) {
            this.ViewModel.AddEmptyProfileDescription();
        }


        private async void SaveProfileDescriptionsButton_Click(object sender, RoutedEventArgs e) {
            await this.ViewModel.SaveProfileDescriptionsAsync();
            this.SaveProfleDescriptionsButton.Visibility = Visibility.Collapsed;
        }

        #region External description display info

        /* these classes recreate the structure of ExternalDescriptionSpecification to work with the default
         * configurations of DataGrid */

        // These are used for UI in SettingsPage via Binding and DisplayMemberPath, here we disable ReSharper's warnings
        // ReSharper disable CollectionNeverQueried.Local
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        // ReSharper disable MemberCanBeMadeStatic.Local

        private class ExternalDescriptionTypeInfo {
            public string Name { get; }
            public ExternalDescriptionType DescriptionType { get; }

            public ExternalDescriptionTypeInfo(string name, ExternalDescriptionType descriptionType) {
                this.Name = name;
                this.DescriptionType = descriptionType;
            }
        }

        private class ExternalDescriptionFileInfo {
            public string Name { get; }
            public ExternalFileType FileType { get; }

            public ExternalDescriptionFileInfo(string name, ExternalFileType fileType) {
                this.Name = name;
                this.FileType = fileType;
            }
        }

        private class ExternalDescriptionFilterInfo {
            public string Name { get; }
            public ExternalDescriptionFilterType FilterType { get; }

            public ExternalDescriptionFilterInfo(string name, ExternalDescriptionFilterType filterType) {
                this.Name = name;
                this.FilterType = filterType;
            }
        }

        private List<ExternalDescriptionTypeInfo> ExternalDescriptionTypes => new List<ExternalDescriptionTypeInfo> {
            new ExternalDescriptionTypeInfo("Text", ExternalDescriptionType.Text),
            new ExternalDescriptionTypeInfo("Link", ExternalDescriptionType.Link)
        };

        private List<ExternalDescriptionFileInfo> ExternalDescriptionFileTypes => new List<ExternalDescriptionFileInfo> {
            new ExternalDescriptionFileInfo("Content", ExternalFileType.Content),
            new ExternalDescriptionFileInfo("File name", ExternalFileType.FileName)
        };

        private List<ExternalDescriptionFilterInfo> ExternalDescriptionFilterTypes => new List<ExternalDescriptionFilterInfo> {
            new ExternalDescriptionFilterInfo("None", ExternalDescriptionFilterType.None),
            new ExternalDescriptionFilterInfo("Regex replace", ExternalDescriptionFilterType.RegexReplace)
        };

        // ReSharper restore CollectionNeverQueried.Local
        // ReSharper restore UnusedAutoPropertyAccessor.Local
        // ReSharper restore MemberCanBeMadeStatic.Local


        #endregion
    }
}
