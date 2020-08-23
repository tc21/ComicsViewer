using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TC.Database;
using TC.Database.MicrosoftSqlite;

namespace ComicsLibrary.SQL {
    public class ComicsReadOnlyManager {
        public ComicsDatabaseConnection Connection { get; }

        internal ComicsReadOnlyManager(SqliteDatabaseConnection databaseConnection) {
            this.Connection = new ComicsDatabaseConnection(databaseConnection);
        }

        public Task<List<Comic>> GetAllComicsAsync() => this.Connection.GetActiveComicsAsync();
        public Task<ComicMetadata?> TryGetMetadataAsync(Comic comic) => this.Connection.TryGetComicMetadataAsync(comic);
        public Task<Dictionary<string, string>> GetAuthorAliasesAsync() => this.Connection.GetAuthorAliasesAsync();
        public Task<Dictionary<string, string>> GetCategoryAliasesAsync() => this.Connection.GetCategoryAliasesAsync();
    }
}
