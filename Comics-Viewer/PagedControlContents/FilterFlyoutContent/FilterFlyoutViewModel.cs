using ComicsLibrary;
using ComicsViewer.Features;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer.ViewModels.Pages {
    public class FilterFlyoutViewModel : ViewModelBase {
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

        // TODO come up with a better name than CountedString?
        public List<CountedString> Categories { get; }
        public List<CountedString> Authors { get; }
        public List<CountedString> Tags { get; }

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

        internal ComicItemGridViewModel ParentViewModel { get; }
        internal Filter Filter { get; }

        public FilterFlyoutViewModel(ComicItemGridViewModel parentViewModel, Filter filter, FilterViewAuxiliaryInfo info) {
            this.ParentViewModel = parentViewModel;
            this.Filter = filter;

            this.Categories = info.Categories.OrderBy(x => x.Name).ToList();
            this.Authors = info.Authors.OrderBy(x => x.Name).ToList();
            this.Tags = info.Tags.Where(x => x.Count > 1).OrderByDescending(x => x.Count).ToList();
        }
    }
}
