using ComicsLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Filters {
    public class Filter {
        private readonly HashSet<string> selectedAuthors = new HashSet<string>();
        private readonly HashSet<string> selectedCategories = new HashSet<string>();
        private readonly HashSet<string> selectedTags = new HashSet<string>();
        private Func<Comic, bool>? generatedFilter;
        private Func<Comic, bool>? search;

        public bool AddSelectedAuthor(string author) => this.AddTo(this.selectedAuthors, author);
        public bool RemoveSelectedAuthor(string author) => this.RemoveFrom(this.selectedAuthors, author);
        public bool AddSelectedCategory(string category) => this.AddTo(this.selectedCategories, category);
        public bool RemoveSelectedCategory(string category) => this.RemoveFrom(this.selectedCategories, category);
        public bool AddSelectedTag(string tag) => this.AddTo(this.selectedTags, tag);
        public bool RemoveSelectedTag(string tag) => this.RemoveFrom(this.selectedTags, tag);

        public FilterMetadata Metadata = new FilterMetadata();

        public Func<Comic, bool>? GeneratedFilter {
            get => this.generatedFilter;
            set { 
                this.generatedFilter = value;
                this.Updated();
            }
        }

        public Func<Comic, bool>? Search {
            get => this.search;
            set {
                this.search = value;
                this.Updated();
            }
        }

        private bool AddTo(HashSet<string> set, string item) {
            var result = set.Add(item);
            this.Updated();
            return result;
        }

        private bool RemoveFrom(HashSet<string> set, string item) {
            var result = set.Remove(item);
            this.Updated();
            return result;
        }

        private void Updated() {
            if (!this.deferNotifications) {
                this.FilterChanged(this);
            }
        }

        public delegate void FilterChangedEventHandler(Filter filter);
        public event FilterChangedEventHandler FilterChanged = delegate { };

        public bool ShouldBeVisible(Comic comic) {
            if (this.selectedAuthors.Count != 0 && !this.selectedAuthors.Contains(comic.DisplayAuthor)) {
                return false;
            }

            if (this.selectedCategories.Count != 0 && !this.selectedCategories.Contains(comic.DisplayCategory)) {
                return false;
            }

            if (this.selectedTags.Count != 0 && comic.Tags.All(tag => !this.selectedCategories.Contains(comic.DisplayCategory))) {
                return false;
            }

            if (this.GeneratedFilter?.Invoke(comic) == false) {
                return false;
            }

            return this.Search?.Invoke(comic) ?? true;
        }

        // Based on Microsoft.Toolkit.Uwp.UI.AdvancedCollectionView
        private bool deferNotifications = false;

        public IDisposable DeferNotifications() {
            return new NotificationDeferrer(this);
        }

        public class NotificationDeferrer : IDisposable {
            private readonly Filter filter;

            public NotificationDeferrer(Filter filter) {
                this.filter = filter;
                this.filter.deferNotifications = true;
            }

            public void Dispose() {
                this.filter.deferNotifications = false;
                this.filter.FilterChanged?.Invoke(this.filter);
            }
        }
    }

    /// <summary>
    /// Contains metadata used by the view model that views can manually assign
    /// </summary>
    public class FilterMetadata {
        public int GeneratedFilterItemCount { get; set; }
        public string SearchPhrase { get; set; } = "";
    }
}
