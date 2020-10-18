using ComicsLibrary.Sorting;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Features {
    // You can nest ApplicationDataContainers infinitely, but we'll only use two levels 
    internal class DefaultSettingsAccessor {
        private readonly Dictionary<string, DefaultSetting> defaults = new Dictionary<string, DefaultSetting>();
        private static ApplicationDataContainer SettingsContainer => ApplicationData.Current.LocalSettings;

        public T Get<T>(string key) {
            this.ValidateValueType(key, typeof(T));

            if (SettingsContainer.Values.TryGetValue(key, out var stored) && stored is T value) {
                return value;
            }

            Debug.WriteLine($"Warning: {nameof(this.Get)} retrieved an invalid stored value and is returning a default");
            return this.GetDefault<T>(key);
        }

        // Note: we could optimize GetCollectionItem and SetCollectionItem, but we'll do that when we have that performance need
        public T GetCollectionItem<T>(string collectionName, string keyName) {
            this.ValidateCollectionType(collectionName, typeof(T));
            this.ValidateCollectionItem<T>(collectionName, keyName);

            if (SettingsContainer.Containers.TryGetValue(collectionName, out var container)
                    && container.Values.TryGetValue(keyName, out var stored)
                    && stored is T value) {
                return value;
            }

            Debug.WriteLine($"Warning: {nameof(this.GetCollectionItem)} retrieved an invalid stored value and is returning a default");
            return this.GetCollectionItemDefault<T>(collectionName, keyName);
        }

        public void Set<T>(string key, T value) {
            this.ValidateValueType(key, typeof(T));
            SettingsContainer.Values[key] = value;
        }

        public void SetCollectionItem<T>(string collectionName, string keyName, T value) {
            this.ValidateCollectionType(collectionName, typeof(T));
            this.ValidateCollectionItem<T>(collectionName, keyName);

            var container = SettingsContainer.CreateContainer(collectionName, ApplicationDataCreateDisposition.Always);
            container.Values[keyName] = value;
        }

        private T GetDefault<T>(string key) {
            this.ValidateValueType(key, typeof(T));
            return (T)this.defaults[key].Value;
        }

        public T GetCollectionItemDefault<T>(string collectionName, string keyName) {
            this.ValidateCollectionType(collectionName, typeof(T));

            var defaults = (IDictionary<string, T>)this.defaults[collectionName].Value;
            if (defaults.TryGetValue(keyName, out var value)) {
                return value;
            }

            throw new ArgumentException($"Could not found key {keyName} in collection {collectionName}");
        }

        // This constructor is not type-checked at all so be diligent
        internal DefaultSettingsAccessor(Dictionary<string, object> defaults) {
            foreach (var (key, value) in defaults) {
                var type = value.GetType();

                if (type.GetInterfaces().Any(t => ImplementsGenericInterface(t, typeof(IDictionary<,>)))) {
                    var valueType = type.GenericTypeArguments[1];
                    this.defaults[key] = new DefaultSetting(DefaultSettingType.Collection, valueType, value);
                } else {
                    this.defaults[key] = new DefaultSetting(DefaultSettingType.Value, type, value);
                }
            }

            static bool ImplementsGenericInterface(Type type, Type interfaceType) {
                return type.IsGenericType && type.GetGenericTypeDefinition() == interfaceType;
            }
        }

        private class DefaultSetting {
            public readonly DefaultSettingType SettingType;
            public readonly Type ItemType;
            public readonly object Value;

            public DefaultSetting(DefaultSettingType settingType, Type itemType, object defaultValue) {
                this.SettingType = settingType;
                this.ItemType = itemType;
                this.Value = defaultValue;
            }
        }

        private enum DefaultSettingType {
            Value, Collection
        }

        private void ValidateValueType(string key, Type type) {
            if (!this.defaults.ContainsKey(key)) {
                throw new ArgumentException($"Invalid key '{key}'");
            }

            if (this.defaults[key].SettingType != DefaultSettingType.Value) {
                throw new ArgumentException("Invalid setting type (expected Value, actually Collection)");
            }

            if (this.defaults[key].ItemType != type) {
                throw new ArgumentException($"Invalid item type (expected '{this.defaults[key].ItemType}', is '{type}')");
            }
        }

        private void ValidateCollectionType(string key, Type type) {
            if (!this.defaults.ContainsKey(key)) {
                throw new ArgumentException($"Invalid key '{key}'");
            }

            if (this.defaults[key].SettingType != DefaultSettingType.Collection) {
                throw new ArgumentException("Invalid setting type (expected Collection, actually Value)");
            }

            if (this.defaults[key].ItemType != type) {
                throw new ArgumentException($"Invalid item type (expected '{this.defaults[key].ItemType}', is '{type}')");
            }
        }

        private void ValidateCollectionItem<T>(string key, string collectionKey) {
            // assumes caller calls ValidateCollectionType
            var defaults = (IDictionary<string, T>)this.defaults[key].Value;
            if (!defaults.ContainsKey(collectionKey)) {
                throw new ArgumentException($"Invalid key '{collectionKey}' for collection '{key}'");
            }
        }
    }


    public static class Defaults {
        private static readonly Dictionary<string, object> DefaultSettings = new Dictionary<string, object> {
            ["LastProfile"] = "",
            ["SortSelections"] = new Dictionary<string, int>() {
                [NavigationTag.Comics.ToTagName()] = (int)ComicSortSelector.Author,
                [NavigationTag.Author.ToTagName()] = (int)ComicPropertySortSelector.Name,
                [NavigationTag.Tags.ToTagName()] = (int)ComicPropertySortSelector.Name,
                [NavigationTag.Category.ToTagName()] = (int)ComicPropertySortSelector.Name,
                [NavigationTag.Playlist.ToTagName()] = (int)ComicPropertySortSelector.Name,
                [NavigationTag.Detail.ToTagName()] =(int)ComicSortSelector.Author
            },
            // Since we can't store a list, we'll use a string joined by '|' for now. Obviously will break if someone searches with the character '|'...
            ["SavedSearches"] = new DefaultDictionary<string, string>(() => ""),
        };

        private static readonly DefaultSettingsAccessor DefaultSettingsAccessor = new DefaultSettingsAccessor(DefaultSettings);

        public static class SettingsAccessor {
            public static string LastProfile {
                get => DefaultSettingsAccessor.Get<string>("LastProfile");
                set => DefaultSettingsAccessor.Set("LastProfile", value);
            }

            public static int DefaultSortSelection(NavigationTag tag)
                => DefaultSettingsAccessor.GetCollectionItemDefault<int>("SortSelections", tag.ToTagName());

            public static int GetLastSortSelection(NavigationTag tag)
                => DefaultSettingsAccessor.GetCollectionItem<int>("SortSelections", tag.ToTagName());

            public static void SetLastSortSelection(NavigationTag tag, int value)
                => DefaultSettingsAccessor.SetCollectionItem("SortSelections", tag.ToTagName(), value);

            public static List<string> GetSavedSearches(string profileName)
                => DefaultSettingsAccessor.GetCollectionItem<string>("SavedSearches", profileName).Split('|').ToList();

            public static void SetSavedSearches(string profileName, IEnumerable<string> searches)
                => DefaultSettingsAccessor.SetCollectionItem("SavedSearches", profileName, string.Join('|', searches.Take(4)));
        }

        public static StorageFolder ApplicationDataFolder => ApplicationData.Current.LocalFolder;

        private static async Task<StorageFolder> GetApplicationDataFolderAsync(string name) {
            try {
                return await ApplicationDataFolder.GetFolderAsync(name);
            } catch (FileNotFoundException) {
                return await ApplicationDataFolder.CreateFolderAsync(name);
            }
        }

        public static async Task<StorageFolder> GetProfileFolderAsync() => await GetApplicationDataFolderAsync("Profiles");
        public static async Task<StorageFolder> GetDatabaseFolderAsync() => await GetApplicationDataFolderAsync("Databases");
        public static async Task<StorageFolder> GetThumbnailFolderAsync() => await GetApplicationDataFolderAsync("Thumbnails");

        public static string DatabaseFileNameForProfile(UserProfile profile) {
            return Path.Combine(ApplicationDataFolder.Path, "Databases", $"{profile.Name}.library.db");
        }
    }
}
