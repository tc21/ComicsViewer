using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsLibrary {
    public class Comic {
        public string Title { get; }
        public string Author { get; }
        public string UniqueIdentifier => $"[{this.Author}]{this.Title}";
        public string Path { get; }
        public string Category { get; }
        private ComicMetadata Metadata { get; }

        public Comic(string path, string title, string author, string category, ComicMetadata? metadata = null) {
            var names = path.Split(System.IO.Path.DirectorySeparatorChar).Select(name => name.Trim()).ToArray();
            if (names.Length < 2) {
                throw new ArgumentException("Invalid path: must have at least two levels");
            }

            if (names[names.Length - 1] != title) {
                throw new ArgumentException("Invalid title: must be the name of the item's folder.");
            }

            if (names[names.Length - 2] != author) {
                throw new ArgumentException("Invalid author: must be the name of the item's parent folder.");
            }

            this.Path = path;
            this.Title = title;
            this.Author = author;
            this.Category = category;
            this.Metadata = metadata ?? new ComicMetadata();
        }

        public string DisplayTitle => this.Metadata.DisplayTitle ?? this.Title;
        public ICollection<string> Tags => this.Metadata.Tags;
        public bool Loved => this.Metadata.Loved;
        public string DateAdded => this.Metadata.DateAdded.Substring(0, "1970-01-01".Length);
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

        public Comic With(string? title = null, string? author = null, string? path = null, string? category = null, ComicMetadata? metadata = null) {
            return new Comic(
                title: title ?? this.Title,
                author: author ?? this.Author,
                path: path ?? this.Path,
                category: category ?? this.Category,
                metadata: metadata ?? this.Metadata
            );
        }

        public Comic WithMetadata(string? displayTitle = null, IEnumerable<string>? tags = null, bool? loved = null,
                                  string? thumbnailSource = null, string? dateAdded = null) {
            var metadata = new ComicMetadata {
                DisplayTitle = displayTitle ?? this.DisplayTitle,
                Loved = loved ?? this.Loved,
                ThumbnailSource = thumbnailSource ?? this.ThumbnailSource,
                DateAdded = dateAdded ?? this.DateAdded
            };

            if (tags != null) {
                metadata.Tags.UnionWith(tags);
            }

            return this.With(metadata: metadata);
        }
    }

    public class ComicMetadata {
        public string? DisplayTitle { get; set; }
        public HashSet<string> Tags { get; set; } = new HashSet<string>();
        public bool Loved { get; set; }
        public string? ThumbnailSource { get; set; }
        public string DateAdded { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    }
}
