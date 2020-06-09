using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Support.Controls {
    public class CheckBoxItem : ViewModelBase {
        public string Name { get; }

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

        public CheckBoxItem(string name, bool isChecked = false) {
            this.Name = name;
            this.isChecked = isChecked;
        }
    }
}
