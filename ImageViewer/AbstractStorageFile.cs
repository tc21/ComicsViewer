using System;
using System.Threading.Tasks;
using Windows.Storage;

#nullable enable

namespace ImageViewer {
    public class AbstractStorageFile {
        public string Path { get; }
        private StorageFile? file;

        private AbstractStorageFile(string path) {
            this.Path = path;
        }

        public static AbstractStorageFile FromStorageFile(StorageFile file) {
            return new AbstractStorageFile(file.Path) {
                file = file
            };
        }

        public static AbstractStorageFile FromPath(string path) {
            return new AbstractStorageFile(path);
        }

        public async Task<StorageFile> File() {
            if (this.file is null) {
                this.file = await StorageFile.GetFileFromPathAsync(this.Path);
            }

            return this.file;
        }
    }
}
