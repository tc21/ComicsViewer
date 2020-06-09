using ComicsLibrary;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Filters {
    public class Filter : DeferredNotify {
        private readonly HashSet<string> selectedAuthors = new HashSet<string>();
        private readonly HashSet<string> selectedCategories = new HashSet<string>();
        private readonly HashSet<string> selectedTags = new HashSet<string>();
        private Func<Comic, bool>? generatedFilter;
        private Func<Comic, bool>? search;

        public bool ContainsAuthor(string author) => this.selectedAuthors.Contains(author);
        public bool AddAuthor(string author) => this.AddTo(this.selectedAuthors, author);
        public bool RemoveAuthor(string author) => this.RemoveFrom(this.selectedAuthors, author);

        public bool ContainsCategory(string category) => this.selectedCategories.Contains(category);
        public bool AddCategory(string category) => this.AddTo(this.selectedCategories, category);
        public bool RemoveCategory(string category) => this.RemoveFrom(this.selectedCategories, category);

        public bool ContainsTag(string tag) => this.selectedTags.Contains(tag);
        public bool AddTag(string tag) => this.AddTo(this.selectedTags, tag);
        public bool RemoveTag(string tag) => this.RemoveFrom(this.selectedTags, tag);

        public FilterMetadata Metadata = new FilterMetadata();

        public Func<Comic, bool>? GeneratedFilter {
            get => this.generatedFilter;
            set { 
                this.generatedFilter = value;
                this.Notify();
            }
        }

        public Func<Comic, bool>? Search {
            get => this.search;
            set {
                this.search = value;
                this.Notify();
            }
        }

        private bool AddTo(HashSet<string> set, string item) {
            var result = set.Add(item);
            this.Notify();
            return result;
        }

        private bool RemoveFrom(HashSet<string> set, string item) {
            var result = set.Remove(item);
            this.Notify();
            return result;
        }

        protected override void Notify() {
            this.FilterChanged(this);
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
    }

    /// <summary>
    /// Contains metadata used by the view model that views can manually assign
    /// </summary>
    public class FilterMetadata {
        public int GeneratedFilterItemCount { get; set; }
        public string SearchPhrase { get; set; } = "";
    }
}
