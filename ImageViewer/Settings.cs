using Windows.Storage;

#nullable enable

namespace ImageViewer {
    internal static class Settings {
        // A (tiny) subset of ComicsViewer.Defaults
        private static ApplicationDataContainer SettingsContainer => ApplicationData.Current.LocalSettings;

        public static T Get<T>(string key, T d = default) {
            if (SettingsContainer.Values.TryGetValue(key, out var stored) && stored is T value) {
                return value;
            } else {
                return d;
            }
        }

        public static void Set<T>(string key, T value) {
            SettingsContainer.Values[key] = value;
        }
    }
}
