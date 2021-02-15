using System;
using System.IO;
using System.Linq;

#nullable enable

namespace ComicsViewer.Support {
    public static class FileTypes {
        private static readonly string[] ImageExtensions = {
            ".bmp", ".gif", ".heic", ".heif", ".j2k", ".jfi", ".jfif", ".jif", ".jp2", ".jpe", ".jpeg", ".jpf",
            ".jpg", ".jpm", ".jpx", ".mj2", ".png", ".tif", ".tiff", ".webp"
        };

        private static readonly string[] MusicExtensions = {
            ".mp3", ".m4a", ".wav", ".flac"
        };

        public static bool IsImage(string fileName) {
            return ImageExtensions.Contains(Path.GetExtension(fileName));
        }

        public static bool IsMusic(string fileName) {
            return MusicExtensions.Contains(Path.GetExtension(fileName));
        }
    }
}
