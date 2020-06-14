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

        public async Task<IEnumerable<Comic>> GetAllComicsAsync() {
            return await this.Connection.GetAllComicsAsync();
        }

        public Task<ComicMetadata?> TryGetMetadataAsync(Comic comic) {
            return this.Connection.TryGetComicMetadataAsync(comic);
        }
    }
}
