using ComicsViewer.ClassExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace ComicsViewer.Profiles {
    public static class ProfileManager {
        public const string ProfileFileNameExtension = ".profile.json";


        public static bool Initialized { get; private set; } = false;
        public static readonly List<string> LoadedProfiles = new List<string>();

        /// <summary>
        /// To be called when the application loads. Loads existing profiles from the application data folder, 
        /// and loads the last used profile.
        /// </summary>
        public static async Task InitializeAsync() {
            await LoadProfilesAsync(await Defaults.GetProfileFolderAsync());
            Initialized = true;
        }

        public static async Task LoadProfilesAsync(StorageFolder profileFolder) {
            var files = await profileFolder.GetFilesAsync();
            foreach (var file in files) {
                if (file.Name.EndsWith(ProfileFileNameExtension)) {
                    try {
                        using var stream = await file.OpenStreamForReadAsync();
                        _ = UserProfile.Deserialize(stream);
                        LoadedProfiles.Add(file.Name.TruncateEnd(ProfileFileNameExtension.Length));
                    } catch (JsonException) {
                        // do nothing
                    }
                }
            }
        }

        public static async Task<UserProfile> LoadProfileAsync(string name) {
            if (!LoadedProfiles.Contains(name)) {
                throw new ApplicationLogicException($"Profile '{name}' is not in the list of loaded profiles");
            }

            var profileFolder = await Defaults.GetProfileFolderAsync();
            var file = await profileFolder.GetFileAsync(name + ProfileFileNameExtension);
            using var stream = await file.OpenStreamForReadAsync();
            return await UserProfile.Deserialize(stream);
        }

        /// <summary>
        /// Creates a new profile, ensuring its name does not already exist in LoadedProfiles 
        /// </summary>
        /// <param name="suggestedName">The suggested name of the profile. 
        /// Will be renamed if a profile with the given name already exists.</param>
        /// <returns>A UserProfile object with a unique name and default values </returns>
        public static UserProfile CreateProfile(string suggestedName = "Untitled Profile") {
            var acceptedName = suggestedName;

            var counter = 0;
            while (LoadedProfiles.Contains(acceptedName)) {
                counter += 1;
                acceptedName = $"{suggestedName} ({counter})";
            }

            return new UserProfile() {
                Name = suggestedName
            };
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
            await UserProfile.Serialize(profile, stream);
        }
    }
}
