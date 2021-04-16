using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComicsLibrary.SQL.Sqlite;
using ComicsLibrary.Collections;

#nullable enable

namespace ComicsLibrary.SQL {
    public class ComicsDatabaseConnection {
        private readonly SqliteDatabaseConnection connection;

        public ComicsDatabaseConnection(SqliteDatabaseConnection connection) {
            this.connection = connection;
        }

        public SqliteTransaction BeginTransaction() => this.connection.Connection.BeginTransaction();

        public async Task<IEnumerable<Comic>> GetActiveComicsAsync() {
            using var reader = await this.GetComicReaderWithConstraintAsync("active", 1);

            var comics = new List<Comic>();

            while (await reader.ReadAsync()) {
                var comic = ReadComicFromRow(reader);
                comics.Add(comic);
            }

            return comics;
        }

        public async Task AddComicAsync(Comic comic) {
            if (await this.HasComicAsync(comic)) {
                throw new ComicsDatabaseException("Attempting to add a comic that already exists");
            }

            await this.AddComicInnerAsync(comic);

            foreach (var tag in comic.Tags) {
                await this.AssociateTagAsync(comic, tag);
            }
        }

        private async Task AddComicInnerAsync(Comic comic) {
            var parameters = new (string, object?)[] {
                ("path", comic.Path),
                ("unique_identifier", comic.UniqueIdentifier),
                ("title", comic.Title),
                ("author", comic.Author),
                ("category", comic.Category),
                ("loved", comic.Loved),
                ("thumbnail_source", comic.ThumbnailSource),
                ("display_title", comic.DisplayTitle),
            }.Where(pair => pair.Item2 != null)
             .ToDictionary(pair => pair.Item1, pair => pair.Item2!);

            // Note: in .net standard, 'is not' doesn't always work property, despite compiling without errors.
            // Avoid using it in ComicsLibrary for now.
            if (!(await this.connection.ExecuteInsertAsync("comics", parameters) is { } comicid)) {
                throw new ComicsDatabaseException("Insertion of comic failed for unknown reasons.");
            }
        }

        public async Task UpdateComicAsync(Comic comic) {
            if (!await this.HasComicAsync(comic)) {
                throw new ComicsDatabaseException("Attempting to update a comic that doesn't exist");
            }

            // ON CONFLICT UPDATE will do it automatically for us
            await this.AddComicInnerAsync(comic);

            // Update tags
            var storedMetadata = await this.TryGetComicMetadataAsync(comic);

            if (!(storedMetadata is { } metadata)) {
                return;
            }

            var delete = metadata.Tags.Except(comic.Tags);
            var add = comic.Tags.Except(metadata.Tags);

            foreach (var tag in delete) {
                await this.UnassociateTagAsync(comic, tag);
            }

            foreach (var tag in add) {
                await this.AssociateTagAsync(comic, tag);
            }
        }

        public async Task InvalidateComicAsync(Comic comic) {
            if (!await this.HasComicAsync(comic)) {
                throw new ComicsDatabaseException("Attempting to invalidate a comic that doesn't exist");
            }

            var rowsChanged = await this.connection.ExecuteNonQueryAsync(
                $"UPDATE comics SET active = 0 WHERE unique_identifier = @uid",
                new Dictionary<string, object> { ["@uid"] = comic.UniqueIdentifier }
            );

            if (rowsChanged == 0) {
                throw new ComicsDatabaseException("Attempting to invalidate a comic that is already inactive");
            }
        }

        public async Task<ComicMetadata?> TryGetComicMetadataAsync(Comic comic) {
            using var reader = await this.GetComicReaderWithConstraintAsync("unique_identifier", comic.UniqueIdentifier);

            if (!reader.HasRows) {
                return null;
            }

            _ = await reader.ReadAsync();
            return ReadComicMetadataFromRow(reader);
        }

        public Task<bool> HasComicAsync(Comic comic) {
            return this.HasPrimaryKeyAsync("comics", "unique_identifier", comic.UniqueIdentifier);
        }

        private async Task AddTagAsync(string tag) {
            // The insertion may be ignored; 
            _ = await this.connection.ExecuteInsertAsync("tags", new Dictionary<string, object> { ["name"] = tag });
        }

        private async Task AssociateTagAsync(Comic comic, string tag) {
            // The insertion may be ignored; 
            _ = await this.connection.ExecuteInsertAsync("tags", new Dictionary<string, object> { ["name"] = tag });
            _ = await this.connection.ExecuteInsertAsync("comic_tags", new Dictionary<string, object> {
                ["comic"] = comic.UniqueIdentifier,
                ["tag"] = tag,
            });
        }

        private async Task UnassociateTagAsync(Comic comic, string tag) {
            var rowsChanged = await this.connection.ExecuteNonQueryAsync(
                $"DELETE FROM comic_tags WHERE comic = @comic AND tag = @tag",
                new Dictionary<string, object> { 
                    ["@comic"] = comic.UniqueIdentifier, 
                    ["@tag"] = tag 
                }
            );

            if (rowsChanged == 0) {
                throw new ComicsDatabaseException("Attempting to unassociate a tag that isn't already associated.");
            }
        }

        private async Task<bool> HasPrimaryKeyAsync(string table, string keyName, object keyValue) {
            var parameters = new Dictionary<string, object> { ["@value"] = keyValue };
            return (await this.connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE {keyName} = @value", parameters)) > 0;
        }

        private static Comic ReadComicFromRow(SqliteDictionaryReader reader) {
            var path = reader.GetString("path");
            var title = reader.GetString("title");
            var author = reader.GetString("author");
            var category = reader.GetString("category");
            var metadata = ReadComicMetadataFromRow(reader);

            var comic = new Comic(path, title, author, category, metadata);
            return comic;
        }

        private static ComicMetadata ReadComicMetadataFromRow(SqliteDictionaryReader reader) {
            var tagList = reader.GetStringOrNull("tag_list");
            var tags = new HashSet<string>(tagList == null ? new string[0] : tagList.Split(','));

            var metadata = new ComicMetadata {
                DisplayTitle = reader.GetStringOrNull("display_title"),
                ThumbnailSource = reader.GetStringOrNull("thumbnail_source"),
                Loved = reader.GetBoolean("loved"),
                Tags = tags,
                DateAdded = reader.GetString("date_added")
            };

            return metadata;
        }

        private static readonly string GetComicWithConstraintsQuery = @"
            SELECT 
                comics.path,
                comics.title,
                comics.author,
                comics.category,
                comics.display_title,
                comics.thumbnail_source,
                comics.loved,
                comics.date_added,
                group_concat(comic_tags.tag) AS tag_list
            FROM
                comics
            LEFT OUTER JOIN 
                comic_tags ON comic_tags.comic = comics.unique_identifier
            WHERE
                comics.{0} = @constraint_value
            GROUP BY
                comics.unique_identifier
        ";

        private static readonly List<string> GetComicQueryColumnNames = new() {
            "path", "title", "author", "category", "display_title", "thumbnail_source", "loved", "date_added", "tag_list"
        };

        private Task<SqliteDictionaryReader> GetComicReaderWithConstraintAsync(string constraintName, object constraintValue) {
            var parameters = new Dictionary<string, object> { ["@constraint_value"] = constraintValue };
            var query = string.Format(GetComicWithConstraintsQuery, constraintName);

            return this.connection.ExecuteReaderAsync(query, GetComicQueryColumnNames, parameters);
        }

        #region Playlists

        public async Task<List<Playlist>> GetAllPlaylistsAsync(ComicList comics) {
            var reader = await this.connection.ExecuteReaderAsync("SELECT playlist, comic FROM playlist_items", new[] { "playlist", "comic" });
            var playlists = new Dictionary<string, List<string>>();

            while (await reader.ReadAsync()) {
                var playlistName = reader.GetString("playlist");

                if (!playlists.ContainsKey(playlistName)) {
                    playlists[playlistName] = new List<string>();
                }

                if (reader.GetStringOrNull("comic") is { } comicUniqueName) {
                    playlists[playlistName].Add(comicUniqueName);
                }
            }

            var actualPlaylists =
                from pair in playlists
                select Playlist.Make(
                    parent: comics,
                    name: pair.Key,
                    uniqueIds: pair.Value.Where(comics.Contains)
                );

            return actualPlaylists.ToList();
        }

        public async Task RemovePlaylistAsync(string name) {
            if (!await this.HasPlaylistAsync(name)) {
                throw new ComicsDatabaseException($"Attempting to remove a playlist named '{name}', but it doesn't exist");
            }

            // playlist_items will be updated by cascade
            var rowsModified = await this.connection.ExecuteNonQueryAsync(
                $"DELETE FROM playlists WHERE name = @name",
                new Dictionary<string, object> { ["@name"] = name }
            );

            if (rowsModified != 1) {
                throw new ComicsDatabaseException($"Failed to remove playlist '{name}'");
            }
        }

        private Task<bool> HasPlaylistAsync(string name) {
            return this.HasPrimaryKeyAsync("playlists", "name", name);
        }

        public async Task AddPlaylistAsync(string name) {
            _ =  await this.connection.ExecuteInsertAsync("playlists", new Dictionary<string, object> {
                ["name"] = name
            });
        }

        public async Task RenamePlaylistAsync(string oldName, string newName) {
            if (oldName == newName) {
                return;
            }

            var rowsModified = await this.connection.ExecuteNonQueryAsync(
                $"UPDATE playlists SET name = @newName WHERE name = @oldName",
                new Dictionary<string, object> {
                    ["@oldName"] = oldName,
                    ["@newName"] = newName
                }
            );

            if (rowsModified != 1) {
                throw new ComicsDatabaseException($"Failed to rename playlist '{oldName}'");
            }
        }

        public async Task AssociateComicWithPlaylistAsync(string playlist, Comic comic) {
            if (!await this.HasComicAsync(comic)) {
                throw new ComicsDatabaseException("Attempting to update a comic that doesn't exist");
            }

            if (!await this.HasPlaylistAsync(playlist)) {
                throw new ComicsDatabaseException("Attempting to update a playlist that doesn't exist");
            }

            var rowid = await this.connection.ExecuteInsertAsync("playlist_items", new Dictionary<string, object> {
                ["playlist"] = playlist,
                ["comic"] = comic.UniqueIdentifier
            });

            if (rowid == null) {
                throw new ComicsDatabaseException($"Failed to update playlist '{playlist}'");
            }
        }

        public async Task UnassociateComicWithPlaylistAsync(string playlist, Comic comic) {
            if (!await this.HasComicAsync(comic)) {
                throw new ComicsDatabaseException("Attempting to update a comic that doesn't exist");
            }

            if (!await this.HasPlaylistAsync(playlist)) {
                throw new ComicsDatabaseException("Attempting to update a playlist that doesn't exist");
            }

            var rowsModified = await this.connection.ExecuteNonQueryAsync(
                $"DELETE FROM playlist_items WHERE playlist = @playlist AND comic = @comic",
                new Dictionary<string, object> {
                    ["@playlist"] = playlist,
                    ["@comic"] = comic.UniqueIdentifier
                }
            );

            if (rowsModified != 1) {
                throw new ComicsDatabaseException($"Failed to update playlist '{playlist}'");
            }
        }

        #endregion
    }
}
