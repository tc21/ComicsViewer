using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable 

namespace ComicsViewer.Features {
    public class UserProfile {
        // fields to be serialized
        public string Name { get; set; } = string.Empty;
        public int ImageHeight { get; set; } = 240;
        public int ImageWidth { get; set; } = 240;
        public List<string> FileExtensions { get; set; } = ImageFileExtensions.ToList();
        public List<NamedPath> RootPaths { get; set; } = new List<NamedPath>();
        public StartupApplicationType StartupApplicationType { get; set; } = StartupApplicationType.OpenFirstFile;

        // generated properties
        [JsonIgnore]
        public string DatabaseFileName => Defaults.DatabaseFileNameForProfile(this);

        // static values and methods
        public static readonly string[] ImageFileExtensions = { ".jpg", ".jpeg", ".png", ".tiff", ".bmp", ".gif" };
        // Although it's probably best practice to allow the user to configure this, I've never needed to do so yet,
        // and considering I'm the only user
        public static readonly string[] IgnoredFilenamePrefixes = { "~", "(" };

        public UserProfile() { }

        public UserProfile(UserProfile copy) {
            this.Name = copy.Name;
            this.ImageHeight = copy.ImageHeight;
            this.ImageWidth = copy.ImageWidth;
            this.FileExtensions = copy.FileExtensions.ToList();
            this.RootPaths = copy.RootPaths.ToList();
            this.StartupApplicationType = copy.StartupApplicationType;
        }

        public static ValueTask<UserProfile> DeserializeAsync(Stream input) {
            return JsonSerializer.DeserializeAsync<UserProfile>(input);
        }

        public static Task SerializeAsync(UserProfile profile, Stream output) {
            return JsonSerializer.SerializeAsync(output, profile);
        }

        // Profile helper methods
        public async Task<IEnumerable<StorageFile>> GetTopLevelFilesForFolderAsync(StorageFolder folder) {
            var files = await folder.GetFilesAsync();
            return files.Where(file => this.FileExtensions.Contains(Path.GetExtension(file.Name)));
        }

        /// <summary>
        /// returns null if this comic contains no files
        /// </summary>
        public async Task<StorageFile?> GetFirstFileForComicAsync(StorageFolder folder) {
            foreach (var file in await this.GetTopLevelFilesForFolderAsync(folder)) {
                return file;
            }

            var subfolders = await folder.GetFoldersAsync();
            foreach (var subfolder in subfolders) {
                foreach (var file in await this.GetTopLevelFilesForFolderAsync(subfolder)) {
                    return file;
                }
            }

            return null;
        }

        public async Task<bool> FolderContainsValidComicAsync(StorageFolder folder) {
            return (await this.GetFirstFileForComicAsync(folder)) != null;
        }
    }
}