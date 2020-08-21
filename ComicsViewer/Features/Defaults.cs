using ComicsLibrary.Sorting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            if (SettingsContainer.Values.ContainsKey(key)) {
                return (T)SettingsContainer.Values[key];
            }

            return this.GetDefault<T>(key);
        }

        public IDictionary<string, T> GetCollection<T>(string key) {
            this.ValidateCollectionType(key, typeof(T));

            if (!SettingsContainer.Containers.ContainsKey(key)) {
                return this.GetCollectionDefault<T>(key);
            }

            var result = new Dictionary<string, T>();
            var container = SettingsContainer.Containers[key];
            var defaults = (Dictionary<string, object>)this.defaults[key].Value;


            foreach (var collectionKey in defaults.Keys) {
                result[collectionKey] = (T)container.Values[collectionKey];
            }

            return result;
        }

        // Note: we could optimize GetCollectionItem and SetCollectionItem, but we'll do that when we have that performance need
        public T GetCollectionItem<T>(string key, string collectionKey) {
            this.ValidateCollectionType(key, typeof(T));
            this.ValidateCollectionItem(key, collectionKey);

            return this.GetCollection<T>(key)[collectionKey];
        }

        public void Set<T>(string key, T value) {
            this.ValidateValueType(key, typeof(T));
            SettingsContainer.Values[key] = value;
        }

        // As an implementation detail, a container must have all its values set at once
        public void SetCollection<T>(string key, IDictionary<string, T> collection) {
            this.ValidateCollectionType(key, typeof(T));
            this.ValidateCollectionKeys(key, collection.Keys);

            var current = this.GetCollection<T>(key);
            foreach (var (collectionKey, value) in collection) {
                current[collectionKey] = value;
            }

            this.WriteCollection(key, current);
        }

        public void SetCollectionItem<T>(string key, string collectionKey, T value) {
            this.ValidateCollectionType(key, typeof(T));
            this.ValidateCollectionItem(key, collectionKey);

            var collection = this.GetCollection<T>(key);
            collection[collectionKey] = value;
            this.WriteCollection(key, collection);
        }

        private void WriteCollection<T>(string key, IDictionary<string, T> collection) {
            var container = SettingsContainer.CreateContainer(key, ApplicationDataCreateDisposition.Always);
            foreach (var (collectionKey, value) in collection) {
                container.Values[collectionKey] = value;
            }
        }

        public T GetDefault<T>(string key) {
            this.ValidateValueType(key, typeof(T));
            return (T)this.defaults[key].Value;
        }

        public IDictionary<string, T> GetCollectionDefault<T>(string key) {
            this.ValidateCollectionType(key, typeof(T));

            var defaults = (Dictionary<string, object>)this.defaults[key].Value;

            var result = new Dictionary<string, T>();

            foreach (var (collectionKey, value) in defaults) {
                result[collectionKey] = (T)value;
            }

            return result;
        }

        public T GetCollectionItemDefault<T>(string key, string collectionKey) {
            this.ValidateCollectionType(key, typeof(T));
            this.ValidateCollectionItem(key, collectionKey);

            var defaults = (Dictionary<string, object>)this.defaults[key].Value;
            return (T)defaults[collectionKey];
        }

        // This constructor is not type-checked at all so be diligent
        internal DefaultSettingsAccessor(Dictionary<string, object> defaults) {
            foreach (var (key, value) in defaults) {
                var type = value.GetType();
                // Note: we actually only allow Dictionary, not just any IDictionary
                if (value is IDictionary dict) {
                    var valueType = type.GenericTypeArguments[1];
                    var defaultValues = new Dictionary<string, object>();
                    foreach (var collectionKey in dict.Keys) {
                        defaultValues.Add((string)collectionKey, dict[collectionKey]);
                    }

                    this.defaults[key] = new DefaultSetting(DefaultSettingType.Collection, valueType, defaultValues);
                } else {
                    this.defaults[key] = new DefaultSetting(DefaultSettingType.Value, type, value);
                }
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

        public enum DefaultSettingType {
            Value, Collection
        }

        private void ValidateValueType(string key, Type type) {
            if (!this.defaults.ContainsKey(key)) {
                throw new ArgumentException($"Invalid key '{key}'");
            }

            if (this.defaults[key].SettingType != DefaultSettingType.Value) {
                throw new ArgumentException($"Invalid setting type (expected Value, actually Collection)");
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
                throw new ArgumentException($"Invalid setting type (expected Collection, actually Value)");
            }

            if (this.defaults[key].ItemType != type) {
                throw new ArgumentException($"Invalid item type (expected '{this.defaults[key].ItemType}', is '{type}')");
            }
        }

        private void ValidateCollectionItem(string key, string collectionKey) {
            // assumes caller calls ValidateCollectionType
            var defaults = (Dictionary<string, object>)this.defaults[key].Value;
            if (!defaults.ContainsKey(collectionKey)) {
                throw new ArgumentException($"Invalid key '{collectionKey}' for collection '{key}'");
            }
        }

        private void ValidateCollectionKeys(string key, IEnumerable<string> keys) {
            // assumes caller calls ValidateCollectionType
            foreach (var collectionKey in keys) {
                this.ValidateCollectionItem(key, collectionKey);
            }
        }
    }


    public static class Defaults {
        private static readonly Dictionary<string, object> defaultSettings = new Dictionary<string, object> {
            ["LastProfile"] = "",
            ["SortSelections"] = new Dictionary<string, int>() {
                ["comics"] = (int)ComicSortSelector.Author,
                ["authors"] = (int)ComicPropertySortSelector.Name,
                ["tags"] = (int)ComicPropertySortSelector.Name,
                ["categories"] = (int)ComicPropertySortSelector.Name,
                ["default"] =(int)ComicSortSelector.Author
            },
            // Since we can't store a list, we'll use a string joined by '|' for now. Obviously will break if someone searches with the character '|'...
            ["SavedSearches"] = "",
        };

        private static readonly DefaultSettingsAccessor defaultSettingsAccessor = new DefaultSettingsAccessor(defaultSettings);

        public static class SettingsAccessor {
            public static string LastProfile {
                get => defaultSettingsAccessor.Get<string>("LastProfile");
                set => defaultSettingsAccessor.Set("LastProfile", value);
            }

            public static int DefaultSortSelection(string pageType)
                => defaultSettingsAccessor.GetCollectionItemDefault<int>("SortSelections", pageType);

            public static int GetLastSortSelection(string pageType)
                => defaultSettingsAccessor.GetCollectionItem<int>("SortSelections", pageType);

            public static void SetLastSortSelection(string pageType, int value)
                => defaultSettingsAccessor.SetCollectionItem("SortSelections", pageType, value);

            public static IList<string> SavedSearches {
                get => defaultSettingsAccessor.Get<string>("SavedSearches").Split('|').ToList();
                set => defaultSettingsAccessor.Set("SavedSearches", string.Join('|', value));
            }
        }

        private static StorageFolder ApplicationDataFolder => ApplicationData.Current.LocalFolder;
        public static StorageFolder TempFolder => ApplicationData.Current.TemporaryFolder;

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

        public static string ProfileFolderPath => Path.Combine(ApplicationDataFolder.Path, "Profiles");
        public static string DatabaseFolderPath => Path.Combine(ApplicationDataFolder.Path, "Databases");
        public static string ThumbnailFolderPath => Path.Combine(ApplicationDataFolder.Path, "Thumbnails");

        public static string DatabaseFileNameForProfile(UserProfile profile) {
            return Path.Combine(DatabaseFolderPath, $"{profile.Name}.library.db");
        }

        public static async Task<StorageFile> CreateTempFileAsync(string desiredName) {
            return await TempFolder.CreateFileAsync(desiredName, CreationCollisionOption.GenerateUniqueName);
        }
    }
}
