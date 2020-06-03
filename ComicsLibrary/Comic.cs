using System;
using System.Collections.Generic;

namespace ComicsLibrary {
    public class Comic {
        public string Path { get; }
        public string Title { get; }
        public string Author { get; }
        public string Category { get; }
        public ComicMetadata Metadata { get; }

        public string UniqueIdentifier => $"[{this.Author}]{this.Title}";

        public Comic(string path, string title, string author, string category, ComicMetadata metadata = null) {
            this.Path = path;
            this.Title = title;
            this.Author = author;
            this.Category = category;
            this.Metadata = metadata;

            if (this.Metadata == null) {
                this.Metadata = new ComicMetadata();
            }
        }

        private const string OldestDate = "1970-01-01 12:00:00";

        public string DisplayTitle => this.Metadata.DisplayTitle ?? this.Title;
        public string DisplayAuthor => this.Metadata.DisplayAuthor ?? this.Author;
        public string DisplayCategory => this.Metadata.DisplayCategory ?? this.Category;
        public ISet<string> Tags => this.Metadata.Tags ?? new HashSet<string>();
        public bool Loved => this.Metadata.Loved;
        public bool Disliked => this.Metadata.Disliked;
        public string DateAdded => this.Metadata.DateAdded ?? OldestDate;
    }

    public class ComicMetadata {
        public string DisplayTitle { get; set; }
        public string DisplayAuthor { get; set; }
        public string DisplayCategory { get; set; }
        public HashSet<string> Tags { get; set; }
        public bool Loved { get; set; }
        public bool Disliked { get; set; }
        public string ThumbnailSource { get; set; }
        public string DateAdded { get; set; }

        public ComicMetadata() {
            // an estimate of the actual dateAdded, assuming the comic is saved immediately, in SQLite's default format
            this.DateAdded = DateTime.Now.ToString("yyyy-mm-dd HH:mm:ss");
        }
    }
}
