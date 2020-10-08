using ComicsViewer.Common;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class CheckBoxItem<T> : ViewModelBase {
        public T Item { get; }

        private bool _isChecked;
        public bool IsChecked {
            get => this._isChecked;
            set {
                if (this._isChecked == value) {
                    return;
                }

                this._isChecked = value;
                this.OnPropertyChanged();
            }
        }

        protected CheckBoxItem(T name, bool isChecked = false) {
            this.Item = name;
            this._isChecked = isChecked;
        }
    }
}
