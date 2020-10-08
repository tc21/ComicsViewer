using System.ComponentModel;
using System.Runtime.CompilerServices;

#nullable enable

namespace ComicsViewer.Common {
    /// <summary>
    /// The view model from which every view model inherits. Because every Page needs its own viewmodel, 
    /// all containing the same application state, we do that in this class, and make every view model inherit from this.
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "") {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
