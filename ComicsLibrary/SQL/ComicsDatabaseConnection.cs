using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TC.Database;
using TC.Database.MicrosoftSqlite;

namespace ComicsLibrary.SQL {
    public class ComicsDatabaseConnection {
        private const string table_comics = "comics";
        private const string table_tags = "tags";
        private const string table_tags_xref = "comic_tags";
        private const string table_progress = "progress";

        private const string key_path = "folder";
        private const string key_unique_id = "unique_name";
        private const string key_title = "title";
        private const string key_author = "author";
        private const string key_category = "category";
        private const string key_display_title = "display_title";
        private const string key_display_author = "display_author";
        private const string key_display_category = "display_category";
        private const string key_thumbnail_source = "thumbnail_source";
        private const string key_loved = "loved";
        private const string key_disliked = "disliked";
        private const string key_active = "active";
        private const string key_date_added = "date_added";

        private const string key_tag_name = "name";
        private const string key_xref_comic_id = "comicid";
        private const string key_xref_tag_id = "tagid";

        private const string key_progress_comicid = "comicid";
        private const string key_progress = "progress";

        private readonly SqliteDatabaseConnection connection;

        public ComicsDatabaseConnection(SqliteDatabaseConnection connection) {
            this.connection = connection;
        }

        public void Open() => this.connection.Connection.Open();
        public async Task OpenAsync() => await this.connection.Connection.OpenAsync();
        public void Close() => this.connection.Connection.Close();

        public async Task<IEnumerable<Comic>> GetAllComicsAsync() {
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

        public async Task<ComicMetadata?> TryGetComicMetadataAsync(Comic comic) {
            using var reader = await this.GetComicReaderWithContraintAsync(key_unique_id, comic.UniqueIdentifier);

            if (!reader.HasRows) {
                return null;
            }

            await reader.ReadAsync();
            return await this.ReadComicMetadataFromRowAsync(reader);
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
                DisplayAuthor = reader.GetStringOrNull(key_display_author),
                DisplayCategory = reader.GetStringOrNull(key_display_category),
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
            "rowid", key_path, key_title, key_author, key_category, key_display_title, key_display_author,
            key_display_category, key_thumbnail_source, key_loved, key_disliked, key_date_added
        };

        private async Task<DictionaryReader<SqliteDataReader>> GetComicReaderWithContraintAsync(string constraintName, object constraintValue) {
            var parameters = new Dictionary<string, object> { { "@" + constraintName, constraintValue } };

            return await this.connection.ExecuteSelectAsync(table_comics, getComicQueryKeys, 
                $" WHERE {constraintName} = @{constraintName}", parameters);
        }
    }
}
