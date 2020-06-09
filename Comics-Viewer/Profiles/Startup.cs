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
using Windows.UI.Popups;

#nullable enable

namespace ComicsViewer.Profiles {
    public enum StartupApplicationType {
        OpenFirstFile
    }

    public static class Startup {
        public static async Task OpenComicAsync(Comic comic, UserProfile profile) {
            switch (profile.StartupApplicationType) {
                case StartupApplicationType.OpenFirstFile:
                    // The if statement checks that the return value is not null
                    if (await profile.GetFirstFileForComicAtPathAsync(comic.Path) is StorageFile file) {
                        // There's no reason for us to wait for the file to actually launch
                        _ = Launcher.LaunchFileAsync(file);
                    }
                    break;
                // TODO More cases
            }
        } 
    }
}
