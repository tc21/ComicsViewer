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
    public class UserProfile {
        // fields to be serialized
        public string Name { get; set; } = string.Empty;
        public int ImageHeight { get; set; } = 240;
        public int ImageWidth { get; set; } = 240;
        public List<string> FileExtensions { get; set; } = ImageFileExtensions.ToList();
        public StartupApplicationType StartupApplicationType { get; set; } = StartupApplicationType.OpenFirstFile;

        // generated properties
        public string DatabaseFileName => Defaults.DatabaseFileNameForProfile(this);

        // static values and methods
        public static readonly string[] ImageFileExtensions = { ".jpg", ".jpeg", ".png", ".tiff", ".bmp", ".gif" };

        public static ValueTask<UserProfile> Deserialize(Stream input) {
            return JsonSerializer.DeserializeAsync<UserProfile>(input);
        }

        public static Task Serialize(UserProfile profile, Stream output) {
            return JsonSerializer.SerializeAsync(output, profile);
        }

        // Profile helper methods
        public async Task<IEnumerable<StorageFile>> FilesForComicAtPath(string path) {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);
            var files = await folder.GetFilesAsync();

            return files.Where(file => this.FileExtensions.Contains(Path.GetExtension(file.Name)));
        }

        /// <summary>
        /// returns null if this comic contains no files
        /// </summary>
        public async Task<StorageFile?> FirstFileForComicAtPath(string path) {
            foreach (var file in await this.FilesForComicAtPath(path)) {
                return file;
            }

            return null;
        }
    }
}