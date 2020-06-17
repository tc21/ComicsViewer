using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class CheckBoxItem<T> : ViewModelBase {
        public T Item { get; }

        private bool isChecked;
        public bool IsChecked {
            get => this.isChecked;
            set {
                if (this.isChecked == value) {
                    return;
                }

                this.isChecked = value;
                this.OnPropertyChanged();
            }
        }

        public CheckBoxItem(T name, bool isChecked = false) {
            this.Item = name;
            this.isChecked = isChecked;
        }
    }
}
