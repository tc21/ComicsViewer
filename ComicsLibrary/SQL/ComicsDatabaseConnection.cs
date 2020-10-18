using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComicsLibrary.SQL.Sqlite;
using ComicsLibrary.Collections;

#nullable enable

namespace ComicsLibrary.SQL {
    public class ComicsDatabaseConnection {
        private const string table_comics = "comics";
        private const string table_tags = "tags";
        private const string table_tags_xref = "comic_tags";
        private const string table_playlists = "playlists";
        private const string table_playlist_items = "playlist_items";

        private const string key_path = "folder";
        private const string key_unique_id = "unique_name";
        private const string key_title = "title";
        private const string key_author = "author";
        private const string key_category = "category";
        private const string key_display_title = "display_title";
        private const string key_thumbnail_source = "thumbnail_source";
        private const string key_loved = "loved";
        private const string key_active = "active";
        private const string key_date_added = "date_added";
        private const string key_playlist_name = "name";

        private const string key_tag_name = "name";
        private const string key_xref_comic_id = "comicid";
        private const string key_xref_tag_id = "tagid";
        private const string key_xref_playlist_id = "playlistid";

        private readonly SqliteDatabaseConnection connection;

        public ComicsDatabaseConnection(SqliteDatabaseConnection connection) {
            this.connection = connection;
        }

        public SqliteTransaction BeginTransaction() => this.connection.Connection.BeginTransaction();

        public async Task<IEnumerable<Comic>> GetActiveComicsAsync() {
            using var reader = await this.GetComicReaderWithConstraintAsync(key_active, 1);

            var comics = new List<Comic>();

            while (await reader.ReadAsync()) {
                var comic = ReadComicFromRow(reader);
                comics.Add(comic);
            }

            return comics;
        }

        /* returns the rowid of the added comic */
        public async Task<int> AddComicAsync(Comic comic) {
            if (await this.HasComicAsync(comic)) {
                throw new ComicsDatabaseException("Attempting to add a comic that already exists");
            }

            var parameters = new (string, object?)[] {
                (key_path, comic.Path),
                (key_unique_id, comic.UniqueIdentifier),
                (key_title, comic.Title),
                (key_author, comic.Author),
                (key_category, comic.Category),
                (key_loved, comic.Loved),
                (key_thumbnail_source, comic.ThumbnailSource),
                (key_display_title, comic.DisplayTitle),
            }.Where(pair => pair.Item2 != null)
             .ToDictionary(pair => pair.Item1, pair => pair.Item2!);

            if (!(await this.connection.ExecuteInsertAsync(table_comics, parameters) is { } comicid)) {
                throw new ComicsDatabaseException("Insertion of comic failed for unknown reasons.");
            }

            foreach (var tag in comic.Tags) {
                await this.AssociateTagAsync(comicid, await this.AddTagAsync(tag));
            }

            return comicid;
        }

        public async Task UpdateComicAsync(Comic comic) {
            if (!(await this.TryGetComicRowidAsync(comic) is { } comicid)) {
                throw new ComicsDatabaseException("Attempting to update a comic that doesn't exist");
            }

            var parameters = new Dictionary<string, object>();

            /* fields to update in a SET clause */
            var setClauses = new List<string>();
            foreach (var (key, value) in new (string, object?)[] {
                (key_path, comic.Path),
                (key_title, comic.Title),
                (key_author, comic.Author),
                (key_category, comic.Category),
                (key_loved, comic.Loved),
                (key_thumbnail_source, comic.ThumbnailSource),
                (key_display_title, comic.DisplayTitle),
                (key_active, true),
            }) {
                if (value != null) {
                    setClauses.Add($"{key} = @{key}");
                    parameters.Add($"@{key}", value);
                }
            }

            var queryString = $@"
                UPDATE {table_comics}
                    SET {string.Join(", ", setClauses)}
                    WHERE rowid = {comicid}";

            _ = await this.connection.ExecuteNonQueryAsync(queryString, parameters);

            // Update tags
            var storedMetadata = await this.TryGetComicMetadataAsync(comic);

            if (!(storedMetadata is { } metadata)) {
                return;
            }

            var delete = metadata.Tags.Except(comic.Tags);
            var add = comic.Tags.Except(metadata.Tags);

            foreach (var tag in delete) {
                if (await this.TryGetComicTagXrefIdAsync(comicid, await this.AddTagAsync(tag)) is { } xrefid) {
                    await this.RemoveRowAsync(table_tags_xref, xrefid);
                }
            }

            foreach (var tag in add) {
                await this.AssociateTagAsync(comicid, await this.AddTagAsync(tag));
            }
        }

        public async Task InvalidateComicAsync(Comic comic) {
            if (!(await this.TryGetComicRowidAsync(comic) is { } comicid)) {
                throw new ComicsDatabaseException("Attempting to invalidate a comic that doesn't exist");
            }

            var rowsChanged = await this.connection.ExecuteNonQueryAsync(
                $"UPDATE {table_comics} SET {key_active} = 0 WHERE rowid = {comicid}");

            if (rowsChanged == 0) {
                throw new ComicsDatabaseException("Attempting to invalidate a comic that is already inactive");
            }
        }

        public async Task<ComicMetadata?> TryGetComicMetadataAsync(Comic comic) {
            using var reader = await this.GetComicReaderWithConstraintAsync(key_unique_id, comic.UniqueIdentifier);

            if (!reader.HasRows) {
                return null;
            }

            _ = await reader.ReadAsync();
            return ReadComicMetadataFromRow(reader);
        }

        public async Task<bool> HasComicAsync(Comic comic) {
            return await this.TryGetComicRowidAsync(comic) != null;
        }

        private async Task<int?> TryGetComicRowidAsync(Comic comic) {
            var constraints = new Dictionary<string, object> { [key_unique_id] = comic.UniqueIdentifier };
            var rowids = await this.GetRowidsAsync(table_comics, constraints);

            return rowids.Count == 1 
                ? rowids[0] 
                : (int?)null;
        }

        /* returns the tag's rowid; can be used to query for an existing tag */
        private async Task<int> AddTagAsync(string tag) {
            // The insertion may be ignored; 
            _ = await this.connection.ExecuteInsertAsync(table_tags, new Dictionary<string, object> { [key_tag_name] = tag });

            return await this.connection.ExecuteScalarAsync<int>(
                $"SELECT rowid FROM {table_tags} WHERE {key_tag_name} = @{key_tag_name}",
                new Dictionary<string, object> { [$"@{key_tag_name}"] = tag }
            );
        }

        private Task AssociateTagAsync(int comicid, int tagid) {
            // I'm assuming you can't do an injection attack with an Int32
            return this.connection.ExecuteInsertAsync(table_tags_xref, new Dictionary<string, object> {
                [key_xref_comic_id] = comicid,
                [key_xref_tag_id] = tagid,
            });
        }

        /* selects for rows matching the constraint, but only returning the rowids */
        private async Task<List<int>> GetRowidsAsync(string tableName, Dictionary<string, object> constraints) {
            var ids = new List<int>();
            var (whereClause, parameters) = ProcessConstraints(constraints);
            var reader = await this.connection.ExecuteSelectAsync(tableName, new[] { "rowid" }, restOfQuery: whereClause, parameters);

            while (await reader.ReadAsync()) {
                ids.Add(reader.GetInt32("rowid"));
            }

            return ids;
        }

        private async Task<int?> TryGetComicTagXrefIdAsync(int comicid, int tagid) {
            var rowids = await this.GetRowidsAsync(
                table_tags_xref,
                new Dictionary<string, object> {
                    [key_xref_comic_id] = comicid,
                    [key_xref_tag_id] = tagid
                }
            );

            if (rowids.Count == 0) {
                return null;
            }

            return rowids[0];
        }

        private async Task RemoveRowAsync(string table, int rowid) {
            var rowsChanged = await this.connection.ExecuteNonQueryAsync($"DELETE FROM {table} WHERE rowid = {rowid}");
            if (rowsChanged == 0) {
                throw new ComicsDatabaseException("Attempting to remove a row that doesn't exist.");
            }
        }

        private static (string query, Dictionary<string, object> parameters) ProcessConstraints(Dictionary<string, object> constraints) {
            var constraintStrings = new List<string>();
            var parameters = new Dictionary<string, object>();

            foreach (var c in constraints) {
                constraintStrings.Add($"{c.Key} = @{c.Key}");
                parameters[$"@{c.Key}"] = c.Value;
            }

            var constraintString = "";
            if (constraintStrings.Count != 0) {
                constraintString = " WHERE " + string.Join(" AND ", constraintStrings);
            }

            return (constraintString, parameters);
        }

        private static Comic ReadComicFromRow(SqliteDictionaryReader reader) {
            var path = reader.GetString(key_path);
            var title = reader.GetString(key_title);
            var author = reader.GetString(key_author);
            var category = reader.GetString(key_category);
            var metadata = ReadComicMetadataFromRow(reader);

            var comic = new Comic(path, title: title, author: author, category: category, metadata: metadata);
            return comic;
        }

        private static ComicMetadata ReadComicMetadataFromRow(SqliteDictionaryReader reader) {
            var tagList = reader.GetStringOrNull(col_tag_list);
            var tags = new HashSet<string>(tagList == null ? new string[0] : tagList.Split(','));

            var metadata = new ComicMetadata {
                DisplayTitle = reader.GetStringOrNull(key_display_title),
                ThumbnailSource = reader.GetStringOrNull(key_thumbnail_source),
                Loved = reader.GetBoolean(key_loved),
                Tags = tags,
                DateAdded = reader.GetString(key_date_added)
            };

            return metadata;
        }

        private const string col_comic_id = "comicid";
        private const string col_tag_list = "tag_list";

        private static readonly string GetComicWithConstraintsQuery = @$"
            SELECT 
                {table_comics}.rowid AS {col_comic_id},
                {table_comics}.{key_path},
                {table_comics}.{key_title},
                {table_comics}.{key_author},
                {table_comics}.{key_category},
                {table_comics}.{key_display_title},
                {table_comics}.{key_thumbnail_source},
                {table_comics}.{key_loved},
                {table_comics}.{key_date_added},
                group_concat({table_tags}.{key_tag_name}) AS {col_tag_list}
            FROM
                {table_comics}
            LEFT OUTER JOIN 
                {table_tags_xref} ON {table_tags_xref}.{key_xref_comic_id} = {table_comics}.rowid
            LEFT OUTER JOIN
                {table_tags} ON {table_tags_xref}.{key_xref_tag_id} = {table_tags}.rowid
            WHERE
                {table_comics}.{{0}} = @constraint_value
            GROUP BY
                {table_comics}.rowid
        ";

        private static readonly List<string> GetComicQueryColumnNames = new List<string> {
            col_comic_id, key_path, key_title, key_author, key_category, key_display_title, key_thumbnail_source,
            key_loved, key_date_added, col_tag_list
        };

        private Task<SqliteDictionaryReader> GetComicReaderWithConstraintAsync(string constraintName, object constraintValue) {
            var parameters = new Dictionary<string, object> { ["@constraint_value"] = constraintValue };
            var query = string.Format(GetComicWithConstraintsQuery, constraintName);

            return this.connection.ExecuteReaderAsync(query, GetComicQueryColumnNames, parameters);
        }

        #region Playlists

        private static readonly string GetAllPlaylistsQuery = @$"
            SELECT
                {table_playlists}.{key_playlist_name},
                {table_comics}.{key_unique_id}
            FROM 
                {table_playlists}
            LEFT OUTER JOIN
                {table_playlist_items} ON {table_playlist_items}.{key_xref_playlist_id} = {table_playlists}.rowid
            LEFT OUTER JOIN
                {table_comics} ON {table_playlist_items}.{key_xref_comic_id} = {table_comics}.rowid
        "; 
        
        private static readonly List<string> GetPlaylistQueryColumnNames = new List<string> {
            key_playlist_name, key_unique_id
        };

        public async Task<List<Playlist>> GetAllPlaylistsAsync(ComicList comics) {
            var reader = await this.connection.ExecuteReaderAsync(GetAllPlaylistsQuery, GetPlaylistQueryColumnNames);
            var playlists = new Dictionary<string, List<string>>();

            while (await reader.ReadAsync()) {
                var playlistName = reader.GetString(key_playlist_name);

                if (!playlists.ContainsKey(playlistName)) {
                    playlists[playlistName] = new List<string>();
                }

                if (reader.GetStringOrNull(key_unique_id) is { } comicUniqueName) {
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

        #endregion
    }
}
