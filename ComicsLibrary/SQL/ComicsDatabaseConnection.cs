using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TC.Database;
using TC.Database.MicrosoftSqlite;

namespace ComicsLibrary.SQL {
    public class ComicsDatabaseConnection {
        private const string table_comics = "comics";
        private const string table_tags = "tags";
        private const string table_tags_xref = "comic_tags";

        private const string key_path = "folder";
        private const string key_unique_id = "unique_name";
        private const string key_title = "title";
        private const string key_author = "author";
        private const string key_category = "category";
        private const string key_display_title = "display_title";
        private const string key_thumbnail_source = "thumbnail_source";
        private const string key_loved = "loved";
        private const string key_disliked = "disliked";
        private const string key_active = "active";
        private const string key_date_added = "date_added";

        private const string key_tag_name = "name";
        private const string key_xref_comic_id = "comicid";
        private const string key_xref_tag_id = "tagid";

        private readonly SqliteDatabaseConnection connection;

        public ComicsDatabaseConnection(SqliteDatabaseConnection connection) {
            this.connection = connection;
        }

        public SqliteTransaction BeginTransaction() => this.connection.Connection.BeginTransaction();

        public void Open() => this.connection.Connection.Open();
        public async Task OpenAsync() => await this.connection.Connection.OpenAsync();
        public void Close() => this.connection.Connection.Close();

        public async Task<IEnumerable<Comic>> GetActiveComicsAsync() {
            using var reader = await this.GetComicReaderWithContraintAsync(key_active, 1);

            var comics = new List<Comic>();

            while (await reader.ReadAsync()) {
                var comic = await this.ReadComicFromRowAsync(reader);
                if (comic != null) {
                    comics.Add(comic);
                }
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
                (key_disliked, comic.Disliked),
                (key_loved, comic.Loved),
                (key_thumbnail_source, comic.Metadata.ThumbnailSource),
                (key_display_title, comic.Metadata.DisplayTitle),
            }.Where(pair => pair.Item2 != null)
             .ToDictionary(pair => pair.Item1, pair => pair.Item2!);

            if (!(await this.connection.ExecuteInsertAsync(table_comics, parameters) is int comicid)) {
                throw new ComicsDatabaseException("Insertion of comic failed for unknown reasons.");
            }

            foreach (var tag in comic.Tags) {
                await this.AssociateTagAsync(comicid, await this.AddTagAsync(tag));
            }

            return comicid;
        }

        public async Task UpdateComicAsync(Comic comic) {
            if (!(await this.TryGetComicRowidAsync(comic) is int comicid)) {
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
                (key_disliked, comic.Disliked),
                (key_loved, comic.Loved),
                (key_thumbnail_source, comic.Metadata.ThumbnailSource),
                (key_display_title, comic.Metadata.DisplayTitle),
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
            if (storedMetadata == null) {
                return;
            }

            var delete = storedMetadata.Tags.Except(comic.Tags);
            var add = comic.Tags.Except(storedMetadata.Tags);

            foreach (var tag in delete) {
                if (await this.TryGetComicTagXrefIdAsync(comicid, await this.AddTagAsync(tag)) is int xrefid) {
                    await this.RemoveRowAsync(table_tags_xref, xrefid);
                }
            }

            foreach (var tag in add) {
                await this.AssociateTagAsync(comicid, await this.AddTagAsync(tag));
            }
        }

        public async Task InvalidateComicAsync(Comic comic) {
            if (!(await this.TryGetComicRowidAsync(comic) is int comicid)) {
                throw new ComicsDatabaseException("Attempting to invalidate a comic that doesn't exist");
            }

            var rowsChanged = await this.connection.ExecuteNonQueryAsync(
                $"UPDATE {table_comics} SET {key_active} = 0 WHERE rowid = {comicid}");

            if (rowsChanged == 0) {
                throw new ComicsDatabaseException("Attempting to invalidate a comic that is already inactive");
            }
        }

        public async Task<ComicMetadata?> TryGetComicMetadataAsync(Comic comic) {
            using var reader = await this.GetComicReaderWithContraintAsync(key_unique_id, comic.UniqueIdentifier);

            if (!reader.HasRows) {
                return null;
            }

            await reader.ReadAsync();
            return await this.ReadComicMetadataFromRowAsync(reader);
        }

        public async Task<bool> HasComicAsync(Comic comic) {
            return (await this.TryGetComicRowidAsync(comic)) != null;
        }

        private async Task<int?> TryGetComicRowidAsync(Comic comic) {
            var constraints = new Dictionary<string, object> { [key_unique_id] = comic.UniqueIdentifier };
            var rowids = await this.GetRowidsAsync(table_comics, constraints);

            return rowids.Count == 1 ? rowids[0] : (int?)null;
        }

        /* returns the tag's rowid; can be used to query for an existing tag */
        public async Task<int> AddTagAsync(string tag) {
            // The insertion may be ignored; 
            _ = await this.connection.ExecuteInsertAsync(table_tags, new Dictionary<string, object> { [key_tag_name] = tag });

            return await this.connection.ExecuteScalarAsync<int>(
                $"SELECT rowid FROM {table_tags} WHERE {key_tag_name} = @{key_tag_name}",
                new Dictionary<string, object> { [$"@{key_tag_name}"] = tag }
            );
        }

        private Task AssociateTagAsync(int comicid, int tagid) {
            // I'm assuming you can't do an injection attack with an Int32
            return this.connection.ExecuteNonQueryAsync(
                $"INSERT INTO {table_tags_xref} ({key_xref_comic_id}, {key_xref_tag_id}) VALUES ({comicid}, {tagid})");
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

        private async Task<Comic> ReadComicFromRowAsync(DictionaryReader<SqliteDataReader> reader) {
            var path = reader.GetString(key_path);
            var title = reader.GetString(key_title);
            var author = reader.GetString(key_author);
            var category = reader.GetString(key_category);
            var metadata = await this.ReadComicMetadataFromRowAsync(reader);

            var comic = new Comic(path, title: title, author: author, category: category, metadata: metadata);
            return comic;
        }

        private async Task<ComicMetadata> ReadComicMetadataFromRowAsync(DictionaryReader<SqliteDataReader> reader) {
            var rowid = reader.GetInt32("rowid");

            var metadata = new ComicMetadata {
                DisplayTitle = reader.GetStringOrNull(key_display_title),
                ThumbnailSource = reader.GetStringOrNull(key_thumbnail_source),
                Loved = reader.GetBoolean(key_loved),
                Disliked = reader.GetBoolean(key_disliked),
                Tags = new HashSet<string>(await this.ReadTagsAsync(rowid)),
                DateAdded = reader.GetString(key_date_added)
            };

            return metadata;
        }

        private async Task<List<string>> ReadTagsAsync(int comicid) {
            var tags = new List<string>();
            var sql = $"SELECT {key_xref_tag_id} FROM {table_tags_xref} WHERE {key_xref_comic_id} = {comicid}";

            using var command = this.connection.CreateCommand(sql);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync()) {
                var tagid = reader.GetInt32(0);
                tags.Add(await this.GetTagAsync(tagid));
            }
        
            return tags;
        }

        private async Task<string> GetTagAsync(int tagid) {
            var sql = $"SELECT {key_tag_name} FROM {table_tags} WHERE rowid = {tagid}";
            return await this.connection.ExecuteScalarAsync<string>(sql);
        }

        private static readonly List<string> getComicQueryKeys = new List<string> {
            "rowid", key_path, key_title, key_author, key_category, key_display_title,
            key_thumbnail_source, key_loved, key_disliked, key_date_added
        };

        private async Task<DictionaryReader<SqliteDataReader>> GetComicReaderWithContraintAsync(string constraintName, object constraintValue) {
            var parameters = new Dictionary<string, object> { [$"@{constraintName}"] = constraintValue };

            return await this.connection.ExecuteSelectAsync(table_comics, getComicQueryKeys, 
                $" WHERE {constraintName} = @{constraintName}", parameters);
        }
    }
}
