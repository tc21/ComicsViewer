using ComicsViewer.Profiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.ViewModels {
    /// <summary>
    /// The view model from which every view model inherits. Because every Page needs its own viewmodel, 
    /// all containing the same application state, we do that in this class, and make every view model inherit from this.
    /// 
    /// This class should not contain any non-static state.
    /// </summary>
    class ViewModel : INotifyPropertyChanged {
        /* static properties */
        public string[] SortSelectors => Sorting.SortSelectorNames;
        public int ImageHeight => this.Profile.ImageHeight;
        public int ImageWidth => this.Profile.ImageWidth;
        public string ProfileName => this.Profile.Name;

        internal readonly UserProfile Profile;

        internal ViewModel(UserProfile profile) {
            this.Profile = profile;
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        internal void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
