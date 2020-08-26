using ComicsLibrary;
using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Features {
    public class Filter : DeferredNotify {
        private readonly HashSet<string> selectedAuthors = new HashSet<string>();
        private readonly HashSet<string> selectedCategories = new HashSet<string>();
        private readonly HashSet<string> selectedTags = new HashSet<string>();
        private Func<Comic, bool>? generatedFilter;
        private Func<Comic, bool>? search;
        private bool onlyShowLoved = false;
        private bool showDisliked = false;

        public bool IsActive => this.selectedAuthors.Count != 0 || this.selectedCategories.Count != 0 
            || this.selectedTags.Count != 0 || this.generatedFilter != null || this.onlyShowLoved || this.ShowDisliked;

        public bool ContainsAuthor(string author) => this.selectedAuthors.Contains(author);
        public bool AddAuthor(string author) => this.AddTo(this.selectedAuthors, author);
        public bool RemoveAuthor(string author) => this.RemoveFrom(this.selectedAuthors, author);
        public void ClearAuthors() => this.Clear(this.selectedAuthors);

        public bool ContainsCategory(string category) => this.selectedCategories.Contains(category);
        public bool AddCategory(string category) => this.AddTo(this.selectedCategories, category);
        public bool RemoveCategory(string category) => this.RemoveFrom(this.selectedCategories, category);
        public void ClearCategories() => this.Clear(this.selectedCategories);

        public bool ContainsTag(string tag) => this.selectedTags.Contains(tag);
        public bool AddTag(string tag) => this.AddTo(this.selectedTags, tag);
        public bool RemoveTag(string tag) => this.RemoveFrom(this.selectedTags, tag);
        public void ClearTags() => this.Clear(this.selectedTags);

        public FilterMetadata Metadata = new FilterMetadata();

        public Func<Comic, bool>? GeneratedFilter {
            get => this.generatedFilter;
            set { 
                if (this.generatedFilter == value) {
                    return;
                }

                this.generatedFilter = value;
                this.SendNotification();
            }
        }

        public Func<Comic, bool>? Search {
            get => this.search;
            set {
                if (this.search == value) {
                    return;
                }

                this.search = value;
                this.SendNotification();
            }
        }

        public bool OnlyShowLoved {
            get => this.onlyShowLoved;
            set {
                if (this.onlyShowLoved == value) {
                    return;
                }

                this.onlyShowLoved = value;
                this.SendNotification();
            }
        }

        public bool ShowDisliked {
            get => this.showDisliked;
            set {
                if (this.showDisliked == value) {
                    return;
                }

                this.showDisliked = value;
                this.SendNotification();
            }
        }

        public void Clear() {
            this.selectedAuthors.Clear();
            this.selectedCategories.Clear();
            this.selectedTags.Clear();
            this.search = null;
            this.generatedFilter = null;
            this.onlyShowLoved = false;
            this.showDisliked = false;
            this.SendNotification();
        }

        private bool AddTo(HashSet<string> set, string item) {
            var result = set.Add(item);
            if (result) {
                this.SendNotification();
            }
            return result;
        }

        private bool RemoveFrom(HashSet<string> set, string item) {
            var result = set.Remove(item);
            if (result) {
                this.SendNotification();
            }
            return result;
        }

        private void Clear(HashSet<string> set) {
            if (set.Count == 0) {
                return;
            }

            set.Clear();
            this.SendNotification();
        }

        protected override void DoNotify() {
            this.FilterChanged(this);
        }

        public delegate void FilterChangedEventHandler(Filter filter);
        public event FilterChangedEventHandler FilterChanged = delegate { };

        public bool ShouldBeVisible(Comic comic) {
            if (this.selectedAuthors.Count != 0 && !this.selectedAuthors.Contains(comic.Author)) {
                return false;
            }

            if (this.selectedCategories.Count != 0 && !this.selectedCategories.Contains(comic.Category)) {
                return false;
            }

            if (this.selectedTags.Count != 0 && comic.Tags.All(tag => !this.selectedTags.Contains(tag))) {
                return false;
            }

            if (this.onlyShowLoved && !comic.Loved) {
                return false;
            }

            if (!this.showDisliked && comic.Disliked) {
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
