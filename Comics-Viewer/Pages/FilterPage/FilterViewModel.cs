using ComicsLibrary;
using ComicsViewer.Support.Controls;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer.Filters {
    public class FilterViewModel : ViewModelBase {
        internal Func<Comic, bool>? GeneratedFilter {
            get => this.Filter.GeneratedFilter;
            set {
                this.Filter.GeneratedFilter = value;
                this.OnPropertyChanged(nameof(this.GeneratedFilterEnabled));
                this.OnPropertyChanged(nameof(this.GeneratedFilterDescription));
            }
        }
        public bool GeneratedFilterEnabled => this.Filter.GeneratedFilter != null;
        public string GeneratedFilterDescription => $"Automatically generated filter ({this.Filter.Metadata.GeneratedFilterItemCount} items)";


        public List<CheckBoxItem> Categories { get; } = new List<CheckBoxItem>();

        private Filter Filter { get; }

        public FilterViewModel(Filter filter, IEnumerable<string> categories) {
            this.Filter = filter;

            foreach (var category in categories) {
                var item = new CheckBoxItem(category, this.Filter.ContainsCategory(category));
                item.PropertyChanged += this.Item_PropertyChanged;
                this.Categories.Add(item);
            }
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            // We cheat a little since the only changable property right now is IsChecked
            var item = (CheckBoxItem)sender;
            if (item.IsChecked) {
                this.Filter.AddCategory(item.Name);
            } else {
                this.Filter.RemoveCategory(item.Name);
            }
        }
    }
}
