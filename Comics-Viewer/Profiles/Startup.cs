using ComicsLibrary;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace ComicsViewer.Profiles {
    public enum StartupApplicationType {
        OpenFirstFile
    }

    static class Startup {
        internal static async Task OpenComic(Comic comic, UserProfile profile) {
            switch (profile.StartupApplicationType) {
                case StartupApplicationType.OpenFirstFile:
                    // The if statement checks that the return value is not null
                    if (await profile.FirstFileForComicAtPath(comic.Path) is StorageFile file) {
                        // There's no reason for us to wait for the file to actually launch
                        _ = Launcher.LaunchFileAsync(file);
                    }

                    break;
            }
            // TODO placeholder
        } 
    }
}
