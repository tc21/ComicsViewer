using ComicsViewer.Profiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels {
    /// <summary>
    /// The view model from which every view model inherits. Because every Page needs its own viewmodel, 
    /// all containing the same application state, we do that in this class, and make every view model inherit from this.
    /// </summary>
    public class ViewModel : ViewModelBase {
        /* static properties */
        public string[] SortSelectors => Sorting.SortSelectorNames;
        public int ImageHeight => this.Profile.ImageHeight;
        public int ImageWidth => this.Profile.ImageWidth;
        public string ProfileName => this.Profile.Name;

        public readonly UserProfile Profile;

        public ViewModel(UserProfile profile) {
            this.Profile = profile;
        }
    }

    public class ViewModelBase : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public void OnPropertyChanged([CallerMemberName] string propertyName = "") {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
