using System;
using System.Collections.Generic;

namespace ComicsLibrary {
    public class Comic {
        /* title and author are part of the comic's identity. to replace these, remove the comic and create a new one */
        public string Title { get; }
        public string Author { get; }
        public string UniqueIdentifier => $"[{this.Author}]{this.Title}";

        /* path, category, and metadata may be set by the viewmodel, as long as it knows to properly save and update stuff */
        public string Path { get; set; }
        public string Category { get; set; }
        public ComicMetadata Metadata { get; set; }

        public Comic(string path, string title, string author, string category, ComicMetadata? metadata = null) {
            this.Path = path;
            this.Title = title;
            this.Author = author;
            this.Category = category;
            this.Metadata = metadata ?? new ComicMetadata();
        }

        public Comic(string path, string title, string author, string category)
            : this(path, title, author, category, new ComicMetadata()) { }

        private const string OldestDate = "1970-01-01 12:00:00";

        public string DisplayTitle => this.Metadata.DisplayTitle ?? this.Title;
        public string DisplayAuthor => this.Author;
        public string DisplayCategory => this.Category;
        public ISet<string> Tags => this.Metadata.Tags ?? new HashSet<string>();
        public bool Loved => this.Metadata.Loved;
        public bool Disliked => this.Metadata.Disliked;
        public string DateAdded => this.Metadata.DateAdded ?? OldestDate;
        public string? ThumbnailSource {
            get {
                if (this.Metadata.ThumbnailSource == null) {
                    return null;
                }
                if (System.IO.Path.IsPathRooted(this.Metadata.ThumbnailSource)) {
                    return this.Metadata.ThumbnailSource;
                }

                return System.IO.Path.Combine(this.Path, this.Metadata.ThumbnailSource);
            }
        }

        public bool IsSame(Comic other) {
            return this.UniqueIdentifier == other.UniqueIdentifier;
        }
    }

    public class ComicMetadata {
        public string? DisplayTitle { get; set; }
        public HashSet<string> Tags { get; set; } = new HashSet<string>();
        public bool Loved { get; set; } = false;
        public bool Disliked { get; set; } = false;
        public string? ThumbnailSource { get; set; }
        public string DateAdded { get; set; }

        public ComicMetadata() {
            // an estimate of the actual dateAdded, assuming the comic is saved immediately, in SQLite's default format
            this.DateAdded = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
