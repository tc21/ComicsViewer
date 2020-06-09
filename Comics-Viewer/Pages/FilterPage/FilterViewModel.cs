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

        public List<string> Categories { get; }
        public List<string> Authors { get; }
        public List<string> Tags { get; }

        public bool OnlyShowLovedChecked {
            get => this.Filter.OnlyShowLoved;
            set {
                this.Filter.OnlyShowLoved = value;
                this.OnPropertyChanged();
            }
        }
        public bool ShowDislikedChecked {
            get => this.Filter.ShowDisliked;
            set {
                this.Filter.ShowDisliked = value;
                this.OnPropertyChanged();
            }
        }

        internal Filter Filter { get; }

        public FilterViewModel(Filter filter, IEnumerable<string>? categories, IEnumerable<string>? authors, IEnumerable<string>? tags) {
            this.Filter = filter;

            this.Categories = categories.OrderBy(x => x).ToList();
            this.Authors = authors.OrderBy(x => x).ToList();
            this.Tags = tags.OrderBy(x => x).ToList();
        }
    }
}
