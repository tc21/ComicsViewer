using ComicsViewer.Profiles;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

#nullable enable

namespace ComicsViewer.Pages {
    public class SettingsPageViewModel : ViewModelBase {
        /* Static properties */
        public string ProfileName => this.profile.Name;

        private readonly UserProfile profile;
        private readonly MainViewModel mainViewModel;

        public SettingsPageViewModel(MainViewModel mainViewModel, UserProfile profile) {
            this.mainViewModel = mainViewModel;
            this.profile = profile;

            this.ProfileSettings = new List<SettingsItemViewModel>() {
                new SettingsItemViewModel(this, "Profile name", () => this.profile.Name),
                // Example of using default constructor vs genericized method
                new SettingsItemViewModel(this, "Image height", () => this.profile.ImageHeight.ToString(),
                    str => this.profile.ImageHeight = int.Parse(str),
                    str => int.TryParse(str, out var i) && i > 40),
                new SettingsItemViewModel(this, "Image width", () => this.profile.ImageWidth.ToString(),
                    str => this.profile.ImageWidth = int.Parse(str),
                    str => int.TryParse(str, out var i) && i > 40)
            };
        }

        public async Task ProfileModifiedAsync() {
            this.mainViewModel.NotifyProfileChanged(ProfileChangeType.SettingsChanged);
            await ProfileManager.SaveProfileAsync(profile);
        }

        public List<SettingsItemViewModel> ProfileSettings { get; }
    }

    public class SettingsItemViewModel : ViewModelBase {
        public string Name { get; }
        public string Value { get; set; }

        public bool IsEditable => this.setValue != null;

        private bool isEditing = false;
        public bool IsEditing {
            get => this.isEditing;
            set {
                this.isEditing = value;
                this.IsUneditingTextBlockVisible = !value;
                this.OnPropertyChanged();
            }
        }
        private bool isInputValid = false;
        public bool IsInputValid {
            get => this.isInputValid;
            set {
                this.isInputValid = value;
                this.OnPropertyChanged();
            }
        }

        private bool isUneditingTextBlockVisible = true;
        public bool IsUneditingTextBlockVisible {
            get => this.isUneditingTextBlockVisible;
            set {
                this.isUneditingTextBlockVisible = value;
                this.OnPropertyChanged();
            }
        }

        private readonly Func<string> getValue;
        private readonly Action<string>? setValue;
        private readonly Func<string, bool>? validateValue;

        private readonly SettingsPageViewModel ParentViewModel;

        public SettingsItemViewModel(
            SettingsPageViewModel parentViewModel,
            string name, Func<string> getValue, Action<string>? setValue = null, Func<string, bool>? validateValue = null
        ) {
            this.ParentViewModel = parentViewModel;

            this.Name = name;
            this.getValue = getValue;
            this.setValue = setValue;
            this.validateValue = validateValue;

            this.Value = this.getValue();
        }

        public void HyperlinkButton_Tapped(object sender, TappedRoutedEventArgs e) {
            this.IsEditing = true;
            this.IsInputValid = false;
        }

        public void TextBox_TextChanged(object sender, TextChangedEventArgs e) {
            var text = ((TextBox)sender).Text;

            this.IsInputValid = this.validateValue?.Invoke(text) ?? true;
        }

        public async void SaveButton_Tapped(object sender, TappedRoutedEventArgs e) {
            this.setValue!(this.Value);
            this.IsEditing = false;
            this.Value = this.getValue();

            this.OnPropertyChanged(nameof(this.Value));

            await this.ParentViewModel.ProfileModifiedAsync();
        }

        public void RevertButton_Tapped(object sender, TappedRoutedEventArgs e) {
            this.IsEditing = false;
            this.Value = this.getValue();

            this.OnPropertyChanged(nameof(this.Value));
        }
    }
}
