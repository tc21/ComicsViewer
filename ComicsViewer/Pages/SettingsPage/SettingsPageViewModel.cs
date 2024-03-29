﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;
using ComicsViewer.Support;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class SettingsPageViewModel : ViewModelBase {
        /* Static properties */
        public string ProfileName => this.profile.Name;

        private UserProfile profile;
        internal readonly MainViewModel MainViewModel;

        // We will directly edit this list. We will need to save the profile and notify others of changes. 
        public ObservableCollection<NamedPath> RootPaths { get; } = new();
        public ObservableCollection<ExternalDescriptionSpecification> ExternalDescriptions { get; } = new();

        public SettingsPageViewModel(MainViewModel mainViewModel, UserProfile profile) {
            this.MainViewModel = mainViewModel;

            this.MainViewModel.ProfileChanged += this.MainViewModel_ProfileChanged;

            this.profile = profile;
            this.SetProfile(profile);
        }

        private void SetProfile(UserProfile profile) {
            this.profile = profile;

            this.ProfileSettings = new List<SettingsItemViewModel>() {
                new(this, "Profile name", () => this.profile.Name),
                new(this, "Image height", () => this.profile.ImageHeight.ToString(),
                    str => this.profile.ImageHeight = int.Parse(str),
                    IsValidImageDimension
                ),
                new(this, "Image width", () => this.profile.ImageWidth.ToString(),
                    str => this.profile.ImageWidth = int.Parse(str),
                    IsValidImageDimension
                ),
                new(this, "File extensions",
                    getValue: () => StringConversions.CommaDelimitedList.ConvertToString(this.profile.FileExtensions),
                    setValue: value => this.profile.FileExtensions = StringConversions.CommaDelimitedList.Convert(value).ToList(),
                    validateValue: StringConversions.CommaDelimitedList.CanConvert
                )
            };

            this.RootPaths.Clear();
            this.RootPaths.AddRange(this.profile.RootPaths);

            this.ExternalDescriptions.Clear();
            this.ExternalDescriptions.AddRange(this.profile.ExternalDescriptions);

            this.OnPropertyChanged(nameof(this.ProfileSettings));
            this.OnPropertyChanged(nameof(this.ProfileName));

            static ValidateResult IsValidImageDimension(string str) {
                if (!int.TryParse(str, out var i)) {
                    return ValidateResult.Err("Please enter a valid integer");
                }

                if (i < 40) {
                    return ValidateResult.Err("Please enter a number no less than 40");
                }

                return ValidateResult.Ok();
            }
        }

        private void MainViewModel_ProfileChanged(MainViewModel sender, ProfileChangedEventArgs e) {
            if (e.ChangeType == ProfileChangeType.ProfileChanged) {
                this.SetProfile(e.NewProfile);
            }
        }

        public async Task ProfileModifiedAsync() {
            this.MainViewModel.NotifyProfileChanged(ProfileChangeType.SettingsChanged);
            await ProfileManager.SaveProfileAsync(this.profile);
        }

        public List<SettingsItemViewModel> ProfileSettings { get; private set; } = new();

        public async Task CreateProfileAsync(string suggestedName, bool copyCurrent = false) {
            var profile = await ProfileManager.CreateProfileAsync(suggestedName, copyCurrent ? this.MainViewModel.Profile : null);
            await this.MainViewModel.SetProfileAsync(profile.Name);
        }

        public void AddEmptyProfileCategory() {
            this.RootPaths.Add(new NamedPath());
        }

        public async Task SaveProfileCategoriesAsync() {
            // Only keep rooted paths
            this.profile.RootPaths.Clear();
            foreach (var item in this.RootPaths) {
                if (Path.IsPathRooted(item.Path)) {
                    this.profile.RootPaths.Add(new NamedPath { Name = item.Name, Path = Path.GetFullPath(item.Path) });
                }
            }

            // We don't actually have to notify this change
            await ProfileManager.SaveProfileAsync(this.profile);

            this.RootPaths.Clear();
            this.RootPaths.AddRange(this.profile.RootPaths);
        }

        public async Task SaveProfileDescriptionsAsync() {
            this.profile.ExternalDescriptions.Clear();
            this.profile.ExternalDescriptions.AddRange(this.ExternalDescriptions);

            await ProfileManager.SaveProfileAsync(this.profile);
        }

        public void AddEmptyProfileDescription() {
            this.ExternalDescriptions.Add(new ExternalDescriptionSpecification());
        }
    }

    public class SettingsItemViewModel : ViewModelBase {
        public string Name { get; }
        public readonly Func<string> GetValue;
        public readonly Func<string, Task>? SetValueAsync;
        public readonly Func<string, ValidateResult>? ValidateValue;

        public SettingsItemViewModel(
            SettingsPageViewModel parentViewModel,
            string name, Func<string> getValue, Action<string>? setValue = null, Func<string, ValidateResult>? validateValue = null
        ) {
            this.Name = name;

            this.GetValue = getValue;

            if (setValue != null) {
                this.SetValueAsync = value => {
                    setValue(value);
                    return parentViewModel.ProfileModifiedAsync();
                };
            }

            this.ValidateValue = validateValue;
        }
    }
}
