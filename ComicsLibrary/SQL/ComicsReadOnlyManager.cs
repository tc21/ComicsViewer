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

        public ComicsReadOnlyManager(SqliteConnection connection) {
            this.Connection = new ComicsDatabaseConnection(new SqliteDatabaseConnection(connection));
        }

        public async Task<IEnumerable<Comic>> AllComics() {
            // TODO: intentionally not working for testing purposes
            return await this.Connection.GetAllComics();
        }
    }
}
