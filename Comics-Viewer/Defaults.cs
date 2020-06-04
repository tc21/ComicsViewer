using ComicsViewer.Profiles;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace ComicsViewer {
    public static class Defaults {
        private static readonly IDictionary<string, object> defaultSettings = new Dictionary<string, object> {
            { "lastProfile", "" },
            { "comicsSortSelection", (int)Sorting.SortSelector.Author },
            { "categoriesSortSelection", (int)Sorting.SortSelector.Title },
            { "authorsSortSelection", (int)Sorting.SortSelector.Title },
            { "tagsSortSelection", (int)Sorting.SortSelector.Title },
            { "defaultSortSelection", (int)Sorting.SortSelector.Title },
        };

        private static readonly ApplicationDataContainer settingsContainer = ApplicationData.Current.RoamingSettings;

        public static T GetSetting<T>(string key) {
            if (!defaultSettings.ContainsKey(key)) {
                throw new ApplicationLogicException($"Key '{key}' does not exist in the defaultSettings dictionary");
            }

            if (defaultSettings[key].GetType() != typeof(T)) {
                throw new ApplicationLogicException($"Invalid type specified for setting '{key}': " +
                                                    $"expected '{defaultSettings[key].GetType()}', got '{typeof(T)}'");
            }

            if (!settingsContainer.Values.ContainsKey(key)) {
                return (T)defaultSettings[key];
            }

            return (T)settingsContainer.Values[key];

        }

        public static void WriteSetting<T>(string key, T value) {
            if (value == null) {
                throw new ApplicationLogicException("WriteSetting cannot accept null arguments.");
            }

            WriteSetting(key, value, typeof(T));
        }

        public static void WriteSetting(string key, object value, Type type) {
            if (!defaultSettings.ContainsKey(key)) {
                throw new ApplicationLogicException($"Key '{key}' does not exist in the defaultSettings dictionary");
            }

            if (defaultSettings[key].GetType() != type) {
                throw new ApplicationLogicException($"Invalid type specified for setting '{key}': " +
                                                    $"expected '{defaultSettings[key].GetType()}', got '{type}'");
            }

            if (value.GetType() != type) {
                throw new ApplicationLogicException($"Invalid type for argument 'value': expected '{type}', got '{value.GetType()}'");
            }

            settingsContainer.Values[key] = value;
        }

        public static class SettingsAccessor {
            public static string LastProfile {
                get => GetSetting<string>("lastProfile");
                set => WriteSetting("lastProfile", value);
            }

            public static int GetLastSortSelection(string pageType) {
                if (!validPageTypes.Contains(pageType)) {
                    throw new ApplicationLogicException($"'{pageType}' is not a valid page type.");
                }

                return GetSetting<int>(pageType + "SortSelection");
            }

            public static void SetLastSortSelection(string pageType, int value) {
                if (!validPageTypes.Contains(pageType)) {
                    throw new ApplicationLogicException($"'{pageType}' is not a valid page type.");
                }

                WriteSetting(pageType + "SortSelection", value);
            }

            static readonly string[] validPageTypes = { "comics", "categories", "authors", "tags", "default" };
        }

        private static StorageFolder ApplicationDataFolder => ApplicationData.Current.LocalFolder;
        public static StorageFolder TempFolder => ApplicationData.Current.TemporaryFolder;

        private static async Task<StorageFolder> ApplicationDataFolderNamed(string name) {
            try {
                return await ApplicationDataFolder.GetFolderAsync(name);
            } catch (FileNotFoundException) {
                return await ApplicationDataFolder.CreateFolderAsync(name);
            }
        }

        public static async Task<StorageFolder> GetProfileFolder() => await ApplicationDataFolderNamed("Profiles");
        public static async Task<StorageFolder> GetDatabaseFolder() => await ApplicationDataFolderNamed("Databases");
        public static async Task<StorageFolder> GetThumbnailFolder() => await ApplicationDataFolderNamed("Thumbnails");

        public static string ProfileFolderPath => Path.Combine(ApplicationDataFolder.Path, "Profiles");
        public static string DatabaseFolderPath => Path.Combine(ApplicationDataFolder.Path, "Databases");
        public static string ThumbnailFolderPath => Path.Combine(ApplicationDataFolder.Path, "Thumbnails");

        public static string DatabaseFileNameForProfile(UserProfile profile) {
            return Path.Combine(DatabaseFolderPath, $"{profile.Name}.library.db");
        }

        public static async Task<StorageFile> CreateTempFile(string desiredName) {
            return await TempFolder.CreateFileAsync(desiredName, CreationCollisionOption.GenerateUniqueName);
        }
    }
}
