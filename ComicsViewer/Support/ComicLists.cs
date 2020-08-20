using ComicsLibrary;
using ComicsViewer.Features;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

#nullable enable

namespace ComicsViewer.Support {
    public class MainComicList : MutableComicCollection {
        private readonly Dictionary<string, Comic> comics = new Dictionary<string, Comic>();
        public readonly Filter Filter = new Filter();

        public MainComicList() : base(null) {
            this.Filter.FilterChanged += this.Filter_FilterChanged;
        }

        private protected override void AddComic(Comic comic)
            => this.comics.Add(comic.UniqueIdentifier, comic);

        private protected override void RemoveComic(Comic comic) {
            if (!this.comics.Remove(comic.UniqueIdentifier)) {
                throw new ArgumentException("comic doesn't exist in this collection");
            }
        }

        private protected override void RefreshComics(IEnumerable<Comic> comics) {
            this.comics.Clear();

            foreach (var comic in comics) {
                this.comics.Add(comic.UniqueIdentifier, comic);
            }

            this.OnComicChanged(this, new ComicChangedEventArgs(ComicChangedType.Refresh));
        }

        public void Add(IEnumerable<Comic> comics) {
            this.AddComics(comics);
            this.OnComicChanged(this, new ComicChangedEventArgs(ComicChangedType.Add, comics));
        }

        public void Remove(IEnumerable<Comic> comics) {
            this.RemoveComics(comics);
            this.OnComicChanged(this, new ComicChangedEventArgs(ComicChangedType.Remove, comics));
        }

        // you should pass in the new list of comics
        public void NotifyModification(IEnumerable<Comic> comics) {
            this.RemoveComics(comics);
            this.AddComics(comics);
            // The purpose of a modified event independent from add or remove is to aid the view models in delivering a better experience.
            this.OnComicChanged(this, new ComicChangedEventArgs(ComicChangedType.Modified, comics));
        }

        public void Refresh(IEnumerable<Comic> comics) {
            this.RefreshComics(comics);
            this.OnComicChanged(this, new ComicChangedEventArgs(ComicChangedType.Refresh));
        }

        public override bool Contains(Comic comic) => this.comics.ContainsKey(comic.UniqueIdentifier);
        public override Comic GetStored(Comic comic) => this.comics[comic.UniqueIdentifier];
        public override int Count() => this.comics.Count();
        public override IEnumerator<Comic> GetEnumerator() => this.comics.Values.GetEnumerator();

        private ComicView? filtered;
        public ComicView Filtered() {
            if (this.filtered == null) {
                this.filtered = new FilteredComicView(this, this.Filter.ShouldBeVisible);
            }

            return this.filtered;
        }

        private void Filter_FilterChanged(Filter filter) {
            this.OnComicChanged(this, new ComicChangedEventArgs(ComicChangedType.Refresh));
        }
    }

    public abstract class ComicView : IEnumerable<Comic> {
        public abstract int Count();
        public abstract bool Contains(Comic comic);
        public abstract Comic GetStored(Comic comic);
        public abstract IEnumerator<Comic> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private protected ComicView(ComicView? trackChangesFrom = null) {
            if (trackChangesFrom != null) {
                trackChangesFrom.ComicChanged += this.TrackChangesFrom_ComicChanged;
            }
        }

        private protected virtual void TrackChangesFrom_ComicChanged(ComicView sender, ComicChangedEventArgs args) {
            this.ComicChanged(this, args);
        }

        public virtual SortedComicView Sorted(SortedComicView.SortSelector sortSelector)
            => new SortedComicView(this, comics: this, sortSelector);
        

        public virtual FilteredComicView Filtered(Func<Comic, bool> filter) 
            => new FilteredComicView(this, filter);
        

        public event ComicChangedEventHandler ComicChanged = delegate { };
        public delegate void ComicChangedEventHandler(ComicView sender, ComicChangedEventArgs args);
        private protected void OnComicChanged(ComicView sender, ComicChangedEventArgs args)
            => this.ComicChanged(sender, args);
    }

    public class FilteredComicView : ComicView {
        private readonly ComicView filteredFrom;
        private readonly Func<Comic, bool> filter;

        internal FilteredComicView(ComicView filteredFrom, Func<Comic, bool> filter) : base(filteredFrom) {
            this.filteredFrom = filteredFrom;
            this.filter = filter;
        }

        public override bool Contains(Comic comic) => this.filter(comic) && this.filteredFrom.Contains(comic);
        public override int Count() => this.filteredFrom.Where(this.filter).Count();
        public override IEnumerator<Comic> GetEnumerator() => this.filteredFrom.Where(this.filter).GetEnumerator();

        public override Comic GetStored(Comic comic) {
            if (!this.filter(comic)) {
                throw new ArgumentException("comic doesn't exist in this collection");
            }

            return this.filteredFrom.GetStored(comic);
        }

        private protected override void TrackChangesFrom_ComicChanged(ComicView sender, ComicChangedEventArgs args) {
            switch (args.Type) {
                case ComicChangedType.Add:
                case ComicChangedType.Remove:
                case ComicChangedType.Modified:
                    propagateAfterFiltering(args);
                    break;
                case ComicChangedType.Refresh:
                    this.OnComicChanged(this, args);
                    break;
                default:
                    throw new ApplicationLogicException($"{nameof(FilteredComicView)}.{nameof(TrackChangesFrom_ComicChanged)}: unhandled switch case");
            }

            void propagateAfterFiltering(ComicChangedEventArgs args) {
                var filtered = args.Comics.Where(this.filter);
                if (filtered.Count() == 0) {
                    return;
                }

                this.OnComicChanged(this, new ComicChangedEventArgs(args.Type, filtered));
            }
        }
    }

    public abstract class MutableComicCollection : ComicView {
        // Both AddComic and RemoveComic should panic if comics already exist/don't exist
        private protected abstract void AddComic(Comic comics);
        private protected abstract void RemoveComic(Comic comics);
        private protected abstract void RefreshComics(IEnumerable<Comic> comics);

        private readonly ComicView? parent;

        private protected MutableComicCollection(ComicView? trackChangesFrom) : base(trackChangesFrom) {
            this.parent = trackChangesFrom;
        }

        private protected virtual void AddComics(IEnumerable<Comic> comics) {
            foreach (var comic in comics) {
                this.AddComic(comic);
            }
        }

        private protected virtual void RemoveComics(IEnumerable<Comic> comics) {
            foreach (var comic in comics) {
                this.RemoveComic(comic);
            }
        }

        private protected override void TrackChangesFrom_ComicChanged(ComicView sender, ComicChangedEventArgs args) {
            switch (args.Type) {
                case ComicChangedType.Add:
                    this.AddComics(args.Comics!);
                    break;
                case ComicChangedType.Remove:
                    this.RemoveComics(args.Comics!);
                    break;
                case ComicChangedType.Modified:
                    this.RemoveComics(args.Comics!);
                    this.AddComics(args.Comics!);
                    break;
                case ComicChangedType.Refresh:
                    this.RefreshComics(sender);
                    break;
                default:
                    throw new ApplicationLogicException($"{nameof(MutableComicCollection)}.{nameof(TrackChangesFrom_ComicChanged)}: unhandled switch case");
            }

            this.OnComicChanged(this, args);
        }
    }

    public class SortedComicView : MutableComicCollection {
        private readonly Dictionary<string, Comic> comicAccessor;  // allows for O(1) access of sortedComics
        private readonly List<Comic> sortedComics;
        private readonly SortSelector sortSelector;

        #region Comparers

        public enum SortSelector {
            Title, Author, DateAdded, Random
        }

        private class TitleComparer : IComparer<Comic> {
            public int Compare(Comic x, Comic y) => CompareComic(x, y);

            public static int CompareComic(Comic x, Comic y) {
                return x.DisplayTitle.ToLowerInvariant().CompareTo(y.DisplayTitle.ToLowerInvariant());
            }
        }

        private class AuthorComparer : IComparer<Comic> {
            public static int CompareComic(Comic x, Comic y) {
                var result = x.DisplayAuthor.ToLowerInvariant().CompareTo(y.DisplayAuthor.ToLowerInvariant());
                if (result != 0) {
                    return result;
                }

                return TitleComparer.CompareComic(x, y);
            }

            public int Compare(Comic x, Comic y) => CompareComic(x, y);
        }

        private class DateAddedComparer : IComparer<Comic> {
            public static int CompareComic(Comic x, Comic y) {
                var result = -(x.DateAdded.CompareTo(y.DateAdded));
                if (result != 0) {
                    return result;
                }

                return AuthorComparer.CompareComic(x, y);
            }

            public int Compare(Comic x, Comic y) => CompareComic(x, y);
        }

        private static IComparer<Comic> MakeComparer(SortSelector sortSelector) {
            return sortSelector switch {
                SortSelector.Title => new TitleComparer(),
                SortSelector.Author => new AuthorComparer(),
                SortSelector.DateAdded => new DateAddedComparer(),
                _ => throw new ApplicationLogicException($"{nameof(SortedComicView)}.{nameof(MakeComparer)}: unexpected sort selector"),
            };
        }

        #endregion

        internal SortedComicView(
            ComicView trackChangesFrom,
            Dictionary<string, Comic> comicAccessor,
            List<Comic> sortedComics,
            SortSelector sortSelector = SortSelector.Title
        ) : base(trackChangesFrom) {
            this.comicAccessor = comicAccessor;
            this.sortedComics = sortedComics;
            this.sortSelector = sortSelector;
        }

        internal SortedComicView(
            ComicView trackChangesFrom,
            IEnumerable<Comic> comics,
            SortSelector sortSelector = SortSelector.Title
        ) : this(trackChangesFrom, comicAccessor: new Dictionary<string, Comic>(), sortedComics: new List<Comic>(), sortSelector) 
        {
            foreach (var comic in comics) {
                this.AddComic(comic);
            }
        }

        public void Sort(SortSelector sortSelector) {
            SortComics(this.sortedComics, sortSelector);
            // TODO we might make a "Sorted" enum, so that children can ignore this event,
            // but you can't change the sort of an invisible grid anyway
            this.OnComicChanged(this, new ComicChangedEventArgs(ComicChangedType.Refresh));
        }

        private static void SortComics(List<Comic> comics, SortSelector sortSelector) {
            if (sortSelector == SortSelector.Random) {
                Sorting.Shuffle(comics);
            } else {
                comics.Sort(MakeComparer(sortSelector));
            }
        }

        // this override optimizes Sorted since we know more than an unsorted collection
        public override SortedComicView Sorted(SortSelector sortSelector) {
            if (sortSelector == this.sortSelector) {
                return this;
            }

            var sorted = new List<Comic>(this.sortedComics);
            SortComics(sorted, sortSelector);

            return new SortedComicView(this, this.comicAccessor, sorted, sortSelector);
        }

        private protected override void AddComic(Comic comic) {
            if (this.Contains(comic)) {
                throw new ArgumentException("comic already exists in this collection");
            }

            int index;

            if (sortSelector == SortSelector.Random) {
                index = App.Randomizer.Next(this.Count() + 1);
            } else {
                index = this.sortedComics.BinarySearch(comic, MakeComparer(this.sortSelector));
                if (index <= 0) {
                    index = ~index;
                }
            }

            this.sortedComics.Insert(index, comic);
            this.comicAccessor[comic.UniqueIdentifier] = comic;
        }

        private protected override void RemoveComic(Comic comic) {
            if (sortSelector == SortSelector.Random) {
                if (!this.sortedComics.Remove(comic)) {
                    throw new ArgumentException("comic doesn't exist in this collection");
                }
            } else {
                var index = this.sortedComics.BinarySearch(comic, MakeComparer(this.sortSelector));
                if (index < 0) {
                    throw new ArgumentException("comic doesn't exist in this collection");
                }
                this.sortedComics.RemoveAt(index);
            }

            _ = this.comicAccessor.Remove(comic.UniqueIdentifier);
        }

        private protected override void RefreshComics(IEnumerable<Comic> comics) {
            this.sortedComics.Clear();
            this.comicAccessor.Clear();

            foreach (var comic in comics) {
                this.AddComic(comic);
            }
        }

        public override bool Contains(Comic comic) => this.comicAccessor.ContainsKey(comic.UniqueIdentifier);
        public override Comic GetStored(Comic comic) => this.comicAccessor[comic.UniqueIdentifier];
        public override int Count() => this.sortedComics.Count;
        public override IEnumerator<Comic> GetEnumerator() => this.sortedComics.GetEnumerator();
    }

    public class ComicChangedEventArgs {
        public readonly ComicChangedType Type;
        public readonly IEnumerable<Comic>? Comics;

        public ComicChangedEventArgs(ComicChangedType type, IEnumerable<Comic>? comics = null) {
            this.Type = type;
            this.Comics = comics;
        }
    }

    public enum ComicChangedType {
        Add, Remove, Modified, Refresh
    }
}
