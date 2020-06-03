using ComicsViewer.ClassExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace ComicsViewer.Profiles {
    static class ProfileManager {
        const string ProfileFileNameExtension = ".profile.json";


        internal static bool Initialized { get; private set; } = false;
        internal static readonly IList<string> LoadedProfiles = new List<string>();

        /// <summary>
        /// To be called when the application loads. Loads existing profiles from the application data folder, 
        /// and loads the last used profile.
        /// </summary>
        internal static async Task Initialize() {
            await LoadProfiles(await Defaults.GetProfileFolder());
            Initialized = true;
        }

        static async Task LoadProfiles(StorageFolder profileFolder) {
            var files = await profileFolder.GetFilesAsync();
            foreach (var file in files) {
                if (file.Name.EndsWith(ProfileFileNameExtension)) {
                    try {
                        using (var stream = await file.OpenStreamForReadAsync()) {
                            _ = UserProfile.Deserialize(stream);
                        }
                        LoadedProfiles.Add(file.Name.TruncateEnd(ProfileFileNameExtension.Length));
                    } catch (JsonException) {
                        // do nothing
                    }
                }
            }
        }

        internal static async Task<UserProfile> LoadProfile(string name) {
            if (!LoadedProfiles.Contains(name)) {
                throw new ApplicationLogicException($"Profile '{name}' is not in the list of loaded profiles");
            }

            var profileFolder = await Defaults.GetProfileFolder();
            var file = await profileFolder.GetFileAsync(name + ProfileFileNameExtension);
            using (var stream = await file.OpenStreamForReadAsync()) {
                return await UserProfile.Deserialize(stream);
            }
        }

        internal static async Task<UserProfile> LoadDefaultProfile() {
            if (LoadedProfiles.Contains(Defaults.SettingsAccessor.LastProfile)) {
                return await LoadProfile(Defaults.SettingsAccessor.LastProfile);
            }

            if (LoadedProfiles.Count > 0) {
                return await LoadProfile(LoadedProfiles[0]);
            }

            throw new ApplicationLogicException("The application in its current state only allows using pre-made profiles.");

            return CreateProfile();
        }

        /// <summary>
        /// Creates a new profile, ensuring its name does not already exist in LoadedProfiles 
        /// </summary>
        /// <param name="suggestedName">The suggested name of the profile. 
        /// Will be renamed if a profile with the given name already exists.</param>
        /// <returns>A UserProfile object with a unique name and default values </returns>
        internal static UserProfile CreateProfile(string suggestedName = "Untitled Profile") {
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

        internal static async Task SaveProfile(UserProfile profile) {
            var fileName = profile.Name + ProfileFileNameExtension;
            var profileFolder = await Defaults.GetProfileFolder();

            StorageFile file;

            try {
                file = await profileFolder.GetFileAsync(fileName);
            } catch (FileNotFoundException) {
                file = await profileFolder.CreateFileAsync(fileName);
            }

            using (var stream = await file.OpenStreamForWriteAsync()) {
                await UserProfile.Serialize(profile, stream);
            }
        }
    }
}
