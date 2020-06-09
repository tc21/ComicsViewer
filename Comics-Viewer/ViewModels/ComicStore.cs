using ComicsLibrary;
using ComicsLibrary.SQL;
using ComicsViewer.Filters;
using ComicsViewer.Profiles;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ViewModels {
    /// <summary>
    /// A comic store stores a list of all comics, from which the application can request a
    /// viewmodel of part or all of the contained comics.
    /// 
    /// For now, everything is read-only.
    /// </summary>
    public class ComicStore {
        public static readonly ComicStore EmptyComicStore = new ComicStore(new UserProfile(), new Comic[0]);

        private readonly UserProfile profile;
        private readonly List<Comic> comics;

        private ComicStore(UserProfile profile, IEnumerable<Comic> comics) {
            this.profile = profile;
            this.comics = comics.ToList();
        }

        public static async Task<ComicStore> CreateComicsStore(UserProfile profile) {
            var databaseConnection = new SqliteConnection($"Filename={profile.DatabaseFileName}");
            var manager = new ComicsReadOnlyManager(databaseConnection);

            await manager.Connection.OpenAsync();
            var comics = await manager.AllComics();
            return new ComicStore(profile, comics);
        }

        // used for FilterPageViewModel
        internal (HashSet<string> categories, HashSet<string> authors, HashSet<string> tags) GetAuxiliaryInfo(Filter? filter) {
            var categories = new HashSet<string>();
            var authors = new HashSet<string>();
            var tags = new HashSet<string>();

            foreach (var comic in this.comics) {
                if (filter != null && !filter.ShouldBeVisible(comic)) {
                    continue;
                }

                categories.Add(comic.DisplayCategory);
                authors.Add(comic.DisplayAuthor);
                foreach (var tag in comic.Tags) {
                    tags.Add(tag);
                }
            }

            return (categories, authors, tags);
        }

        private IEnumerable<ComicItem> FilterAndGroupComicItems(Filter? filter, Func<Comic, IEnumerable<string>>? groupBy) {
            IEnumerable<Comic> comics = this.comics;
            if (filter != null) {
                comics = comics.Where(filter.ShouldBeVisible);
            }

            var comicItems = new List<ComicItem>();

            if (groupBy == null) {
                return comics.Select(comic => new ComicWorkItem(comic));
            } else {
                return GroupByMultiple(comics, groupBy);
            }
        }

        public IEnumerable<ComicItem> ComicItemsForPage(Filter? filter, string pageType = "comics") { 
            return pageType switch {
                "comics" => this.FilterAndGroupComicItems(filter, null),
                "authors" => this.FilterAndGroupComicItems(filter, comic => new[] { comic.DisplayAuthor }),
                "categories" => this.FilterAndGroupComicItems(filter, comic => new[] { comic.DisplayCategory }),
                "tags" => this.FilterAndGroupComicItems(filter, comic => comic.Tags),
                _ => throw new ApplicationLogicException($"Invalid page type '{pageType}' when creating comic store."),
            };
        }

        private static IEnumerable<ComicNavigationItem> GroupByMultiple(IEnumerable<Comic> comics, Func<Comic, IEnumerable<string>> groupBy) {
            var dict = new Dictionary<string, List<Comic>>();

            foreach (var comic in comics) {
                foreach (var key in groupBy(comic)) {
                    if (!dict.ContainsKey(key)) {
                        dict[key] = new List<Comic>();
                    }

                    dict[key].Add(comic);
                }
            }

            foreach (var pair in dict) {
                yield return new ComicNavigationItem(pair.Key, pair.Value);
            }
        }
    }
}
