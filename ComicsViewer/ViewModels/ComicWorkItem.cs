﻿using System;
using System.Collections.Generic;
using System.Linq;
using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using ComicsViewer.Features;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicWorkItem : ComicItem {
        public Comic Comic { get; private set; }

        public override string Title => this.Comic.DisplayTitle;
        public override string Subtitle => this.Comic.Author;
        public override bool IsLoved => this.Comic.Loved;

        public override IEnumerable<Comic> ContainedComics() => new[] { this.Comic };

        private readonly ComicList owner;

        public ComicWorkItem(Comic comic, ComicList owner) {
            this.Comic = comic;
            this.ThumbnailImageSource = new Uri(Thumbnail.ThumbnailPath(this.Comic));

            this.owner = owner;
            this.owner.ComicsChanged += this.View_ComicsChanged;
        }

        private async void View_ComicsChanged(ComicView sender, ComicsChangedEventArgs e) {
            switch (e.Type) {  // switch ChangeType
                case ComicChangeType.ItemsChanged:
                    // Added: handled by ComicItemGrid

                    if (e.Modified.Any()) {
                        var match = e.Modified.Where(comic => comic.IsSame(this.Comic)).FirstOrNull();

                        if (match is { } comic) {
                            this.Comic = comic;
                            this.OnPropertyChanged("");

                            // an item can't be modified and removed at the same time
                            return;
                        }
                    }

                    if (!e.Removed.Contains(this.Comic)) {
                        return;
                    }

                    this.owner.ComicsChanged -= this.View_ComicsChanged;
                    this.RequestingRefresh(this, RequestingRefreshType.Remove);
                    return;

                case ComicChangeType.Refresh:
                    // the parent will have called refresh, so we don't need to do anything.
                    return;

                case ComicChangeType.ThumbnailChanged:
                    if (!e.Modified.Contains(this.Comic)) {
                        return;
                    }

                    await this.RefreshImageSourceAsync();
                    return;

                default:
                    throw new ProgrammerError($"{nameof(this.View_ComicsChanged)}: unhandled switch case");
            }
        }

        public delegate void RequestingRefreshEventHandler(ComicWorkItem sender, RequestingRefreshType type);
        public event RequestingRefreshEventHandler RequestingRefresh = delegate { };
        public enum RequestingRefreshType {
            Remove
        }
    }
}
