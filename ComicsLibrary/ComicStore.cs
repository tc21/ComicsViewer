using ComicsLibrary.SQL;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ComicsLibrary {
    /// <summary>
    /// Provides quick filtering and selecting of comics at the cost of (probably) more memory usage
    /// </summary>
    public class SComicStore {
        public IReadOnlyList<Comic> Comics { get; }
        public ComicCategoryList DisplayAuthors { get; }
        public ComicCategoryList DisplayCategories { get; }
        public ComicCategoryList Tags { get; }

        public SComicStore(IEnumerable<Comic> comics) {
            var validationSet = new HashSet<string>();

            var comicList = new List<Comic>();
            var authors = new Dictionary<string, List<Comic>>();
            var categories = new Dictionary<string, List<Comic>>();
            var tags = new Dictionary<string, List<Comic>>();

            foreach (var comic in comics) {
                if (validationSet.Contains(comic.UniqueIdentifier)) {
                    throw new ArgumentException("Cannot initialize ComicStore with duplicate comics.");
                }

                validationSet.Add(comic.UniqueIdentifier);
                comicList.Add(comic);

                AddToDictionary(authors, comic.DisplayAuthor, comic);
                AddToDictionary(categories, comic.DisplayCategory, comic);

                foreach (var tag in comic.Tags) {
                    AddToDictionary(tags, tag, comic);
                }
            }

            this.Comics = comicList.AsReadOnly();
            this.DisplayAuthors = new ComicCategoryList(authors);
            this.DisplayCategories = new ComicCategoryList(categories);
            this.Tags = new ComicCategoryList(tags);

            static void AddToDictionary(Dictionary<string, List<Comic>> dict, string key, Comic value) {
                if (!dict.ContainsKey(key)) {
                    dict[key] = new List<Comic>();
                }

                dict[key].Add(value);
            }
        }
    }

    public class ComicCategory : IReadOnlyList<Comic> {
        private readonly List<Comic> comics;

        public string Name { get; }

        public Comic this[int index] => this.comics[index];
        public int Count => this.comics.Count;

        internal ComicCategory(string name, List<Comic> comics) {
            this.Name = name;
            this.comics = comics;
        }

        public IEnumerator<Comic> GetEnumerator() => this.comics.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.comics).GetEnumerator();
    }

    public class ComicCategoryList : IReadOnlyList<ComicCategory> {
        private readonly List<ComicCategory> comicCategories;

        public ComicCategory this[int index] => this.comicCategories[index];
        public int Count => this.comicCategories.Count;

        internal ComicCategoryList(Dictionary<string, List<Comic>> input) {
            this.comicCategories = input.Select(pair => new ComicCategory(pair.Key, pair.Value)).ToList();
        }

        internal ComicCategoryList(List<ComicCategory> cc) {
            this.comicCategories = cc;
        }

        public IEnumerator<ComicCategory> GetEnumerator() => this.comicCategories.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.comicCategories).GetEnumerator();
    }
}
