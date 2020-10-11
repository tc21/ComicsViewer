using ComicsLibrary.SQL;
using ComicsLibrary.SQL.Sqlite;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Uwp.Common;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Features {
    public static class ProfileManager {
        private const string ProfileFileNameExtension = ".profile.json";

        public static bool Initialized { get; private set; }
        public static readonly List<string> LoadedProfiles = new List<string>();

        /// <summary>
        /// To be called when the application loads. Loads existing profiles from the application data folder, 
        /// and loads the last used profile.
        /// </summary>
        public static async Task InitializeAsync() {
            await LoadProfilesAsync(await Defaults.GetProfileFolderAsync());
            Initialized = true;
        }

        private static async Task LoadProfilesAsync(StorageFolder profileFolder) {
            var files = await profileFolder.GetFilesInNaturalOrderAsync();
            foreach (var file in files) {
                if (!file.Name.EndsWith(ProfileFileNameExtension)) {
                    continue;
                }

                try {
                    using var stream = await file.OpenStreamForReadAsync();
                    _ = UserProfile.DeserializeAsync(stream);
                    LoadedProfiles.Add(file.Name.TruncateEnd(ProfileFileNameExtension.Length));
                } catch (JsonException) {
                    // do nothing
                }
            }
        }

        public static async Task<UserProfile> LoadProfileAsync(string name) {
            if (!LoadedProfiles.Contains(name)) {
                throw new ProgrammerError($"Profile '{name}' is not in the list of loaded profiles");
            }

            var profileFolder = await Defaults.GetProfileFolderAsync();
            var file = await profileFolder.GetFileAsync(name + ProfileFileNameExtension);
            using var stream = await file.OpenStreamForReadAsync();
            return await UserProfile.DeserializeAsync(stream);
        }

        /// <summary>
        /// Creates a new profile, ensuring its name does not already exist in LoadedProfiles 
        /// </summary>
        /// <param name="suggestedName">The suggested name of the profile. 
        /// Will be renamed if a profile with the given name already exists.</param>
        /// <param name="copyOf">Copy the settings of this profile</param>
        /// <returns>A UserProfile object with a unique name and default values </returns>
        public static async Task<UserProfile> CreateProfileAsync(string suggestedName = "Untitled Profile", UserProfile? copyOf = null) {
            // make sure acceptedName doesn't already exist by adding a counter repeatedly
            var acceptedName = suggestedName;

            var counter = 0;
            while (LoadedProfiles.Contains(acceptedName)) {
                counter += 1;
                acceptedName = $"{suggestedName} ({counter})";
            }

            var profile = copyOf != null
                ? new UserProfile(copyOf)
                : new UserProfile();

            profile.Name = acceptedName;

            /* save the profile json to disk, and create a new database file */
            await SaveProfileAsync(profile);
            LoadedProfiles.Add(profile.Name);

            // This call here to ensure the database folder is created if it doesn't already exist
            _ = await Defaults.GetDatabaseFolderAsync();
            using var connection = new SqliteConnection($"Filename={profile.DatabaseFileName}");
            await connection.OpenAsync();
            _ = await ComicsManager.InitializeComicsManagerAsync(new SqliteDatabaseConnection(connection));

            return profile;
        }

        public static async Task SaveProfileAsync(UserProfile profile) {
            var fileName = profile.Name + ProfileFileNameExtension;
            var profileFolder = await Defaults.GetProfileFolderAsync();

            StorageFile file;

            try {
                file = await profileFolder.GetFileAsync(fileName);
            } catch (FileNotFoundException) {
                file = await profileFolder.CreateFileAsync(fileName);
            }

            using var stream = await file.OpenStreamForWriteAsync();
            // calling SetLength(0) clears the file content
            stream.SetLength(0);
            await UserProfile.SerializeAsync(profile, stream);
        }
    }
}
