using System;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace MusicPlayer {
    public class PlaylistItem {
        public string Name { get; }
        public int Duration { get; }
        public StorageFile File { get; }
        public string DurationString {
            get {
                var dur = this.Duration;

                if (dur < 60) {
                    return $"0:{dur:D2}";
                }

                var str = "";

                while (dur >= 60) {
                    str = $":{dur % 60:D2}" + str;
                    dur /= 60;
                }

                return $"{dur}{str}";
            }
        }

        private PlaylistItem(string name, int duration, StorageFile file) {
            this.Name = name;
            this.Duration = duration;
            this.File = file;
        }

        public static async Task<PlaylistItem> FromFileAsync(StorageFile file) {
            var properties = await file.Properties.GetMusicPropertiesAsync();
            var duration = (int)properties.Duration.TotalSeconds;

            return new PlaylistItem(file.Name, duration, file);
        }
    }
}
