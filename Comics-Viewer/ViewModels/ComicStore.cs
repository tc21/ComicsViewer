﻿using ComicsLibrary;
using ComicsLibrary.SQL;
using ComicsViewer.Profiles;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.ViewModels {
    /// <summary>
    /// A comic store stores a list of all comics, from which the application can request a
    /// viewmodel of part or all of the contained comics.
    /// 
    /// For now, everything is read-only.
    /// </summary>
    class ComicStore {
        private readonly UserProfile profile;
        private readonly List<Comic> comics;

        private ComicStore(UserProfile profile, IEnumerable<Comic> comics) {
            this.profile = profile;
            this.comics = comics.ToList();
        }

        internal static async Task<ComicStore> CreateComicsStore(UserProfile profile) {
            var databaseConnection = new SqliteConnection($"Filename={profile.DatabaseFileName}");
            var manager = new ComicsReadOnlyManager(databaseConnection);

            await manager.Connection.OpenAsync();
            var comics = await manager.AllComics();
            return new ComicStore(profile, comics);
        }

        private ComicViewModel CreateViewModel(Func<Comic, bool> search, Func<Comic, IEnumerable<string>> groupBy, string pageType) {
            IEnumerable<Comic> comics = this.comics;
            if (search != null) {
                comics = comics.Where(search);
            }

            var comicItems = new List<ComicItem>();

            if (groupBy == null) {
                return new ComicViewModel(this.profile, comics.Select(comic => new ComicWorkItem(comic)), pageType);
            } else {
                return new ComicViewModel(this.profile, GroupByMultiple(comics, groupBy), pageType);
            }
        }

        internal ComicViewModel CreateViewModelForPage(Func<Comic, bool> search, string pageType = "comics") {
            switch (pageType) {
                case "comics":
                    return this.CreateViewModel(search, null, pageType);
                case "authors":
                    return this.CreateViewModel(search, comic => new[] { comic.DisplayAuthor }, pageType);
                case "categories":
                    return this.CreateViewModel(search, comic => new[] { comic.DisplayCategory }, pageType);
                case "tags":
                    return this.CreateViewModel(search, comic => comic.Tags, pageType);
                default:
                    throw new ApplicationLogicException($"Invalid page type '{pageType}' when creating comic store.");
            }
        }

        internal ComicViewModel CreateViewModelForComics(IEnumerable<Comic> comics) {
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
