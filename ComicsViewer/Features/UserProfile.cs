using ComicsLibrary;
using ComicsViewer.Uwp.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable 

namespace ComicsViewer.Features {
    public class UserProfile {
        // fields to be serialized

        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
        // ReSharper disable MemberCanBePrivate.Global

        public string Name { get; set; } = string.Empty;
        public int ImageHeight { get; set; } = 240;
        public int ImageWidth { get; set; } = 240;
        public List<string> FileExtensions { get; set; } = ImageFileExtensions.ToList();
        [JsonConverter(typeof(RootPathsJsonConverter))]
        public RootPaths RootPaths { get; set; } = new RootPaths();
        public StartupApplicationType StartupApplicationType { get; set; } = StartupApplicationType.OpenFirstFile;
        public List<ExternalDescriptionSpecification> ExternalDescriptions { get; set; } = new List<ExternalDescriptionSpecification>();

        // ReSharper restore AutoPropertyCanBeMadeGetOnly.Global
        // ReSharper restore MemberCanBePrivate.Global

        // generated properties
        [JsonIgnore]
        public string DatabaseFileName => Defaults.DatabaseFileNameForProfile(this);

        // static values and methods
        public static readonly string[] ImageFileExtensions = { ".jpg", ".jpeg", ".png", ".tiff", ".bmp", ".gif" };
        // Although it's probably best practice to allow the user to configure this, I've never needed to do so yet,
        // and considering I'm the only user
        private static readonly string[] IgnoredFilenamePrefixes = { "~", "(" };

        public static bool IsImage(string path) {
            return ImageFileExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsIgnoredFolder(StorageFolder folder) {
            return IgnoredFilenamePrefixes.Any(prefix => folder.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        public UserProfile() { }

        public UserProfile(UserProfile copy) {
            this.Name = copy.Name;
            this.ImageHeight = copy.ImageHeight;
            this.ImageWidth = copy.ImageWidth;
            this.FileExtensions = copy.FileExtensions.ToList();
            this.RootPaths = new RootPaths(copy.RootPaths);
            this.StartupApplicationType = copy.StartupApplicationType;
        }

        public static ValueTask<UserProfile> DeserializeAsync(Stream input) {
            return JsonSerializer.DeserializeAsync<UserProfile>(input);
        }

        public static Task SerializeAsync(UserProfile profile, Stream output) {
            return JsonSerializer.SerializeAsync(output, profile);
        }

        // Profile helper methods
        private async Task<IEnumerable<StorageFile>> GetTopLevelFilesForFolderAsync(StorageFolder folder) {
            var files = await folder.GetFilesInNaturalOrderAsync();
            return files.Where(file => this.FileExtensions.Contains(Path.GetExtension(file.Name)));
        }

        /// <summary>
        /// returns null if this comic contains no files
        /// </summary>
        private async Task<StorageFile?> GetFirstFileForComicAsync(StorageFolder folder) {
            foreach (var file in await this.GetTopLevelFilesForFolderAsync(folder)) {
                return file;
            }

            var subfolders = await folder.GetFoldersInNaturalOrderAsync();
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

        // Temporary: this code should be moved elsewhere
        // Unfortunately we aren't actually on .NET Core 3.0, meaning we can't await an IAsyncEnumerable
        public async Task<IEnumerable<ComicSubitem>> GetComicSubitemsAsync(Comic comic) {
            // We currently recurse one level. More levels may be desired in the future...
            var subitems = new List<ComicSubitem>();
            var comicFolder = await StorageFolder.GetFolderFromPathAsync(comic.Path);

            if (await this.ComicSubitemForFolderAsync(comic, comicFolder, "(root item)") is { } rootItem) {
                subitems.Add(rootItem);
            }

            var childrenSubitemTasks = (await comicFolder.GetFoldersInNaturalOrderAsync()).Select(f => this.ComicSubitemForFolderAsync(comic, f));
            var childrenSubitems = (await Task.WhenAll(childrenSubitemTasks)).OfType<ComicSubitem>();

            subitems.AddRange(childrenSubitems);

            if (this.StartupApplicationType == StartupApplicationType.BuiltinViewer && subitems.Count > 1) {
                var allFiles = subitems.SelectMany(s => s.Files).ToList();
                subitems.Insert(0, new ComicSubitem(comic, "(all items)", allFiles));
            }

            return subitems;
        }

        private async Task<ComicSubitem?> ComicSubitemForFolderAsync(Comic comic, StorageFolder folder, string? displayName = null) {
            var files = (await this.GetTopLevelFilesForFolderAsync(folder)).ToList();

            if (!files.Any()) {
                return null;
            }

            return new ComicSubitem(comic, displayName ?? folder.Name, files);
        }
    }
}