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

        private ComicViewModel CreateViewModel(Filter? filter, Func<Comic, IEnumerable<string>>? groupBy, string pageType) {
            IEnumerable<Comic> comics = this.comics;
            if (filter != null) {
                comics = comics.Where(filter.ShouldBeVisible);
            }

            var comicItems = new List<ComicItem>();

            if (groupBy == null) {
                return new ComicViewModel(this.profile, comics.Select(comic => new ComicWorkItem(comic)), pageType);
            } else {
                return new ComicViewModel(this.profile, GroupByMultiple(comics, groupBy), pageType);
            }
        }

        public ComicViewModel CreateViewModelForPage(Filter? filter, string pageType = "comics") {
            return pageType switch {
                "comics" => this.CreateViewModel(filter, null, pageType),
                "authors" => this.CreateViewModel(filter, comic => new[] { comic.DisplayAuthor }, pageType),
                "categories" => this.CreateViewModel(filter, comic => new[] { comic.DisplayCategory }, pageType),
                "tags" => this.CreateViewModel(filter, comic => comic.Tags, pageType),
                _ => throw new ApplicationLogicException($"Invalid page type '{pageType}' when creating comic store."),
            };
        }

        public ComicViewModel CreateViewModelForComics(IEnumerable<Comic> comics) {
            return new ComicViewModel(this.profile, comics.Select(comic => new ComicWorkItem(comic)), "default");
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
