using ComicsLibrary.SQL.Migrations;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TC.Database;
using TC.Database.MicrosoftSqlite;

namespace ComicsLibrary.SQL {
    using SqliteManagedDatabaseConnection
        = ManagedDatabaseConnection<SqliteDatabaseConnection, SqliteConnection, SqliteCommand, SqliteDataReader>;

    public class ComicsManager : ComicsReadOnlyManager {
        public ComicsManager(SqliteDatabaseConnection databaseConnection) : base(databaseConnection) { }

        /* called when a profile is first loaded */
        public static ComicsManager MigratedComicsManager(SqliteDatabaseConnection connection) {
            var manager = new SqliteManagedDatabaseConnection(connection, ComicsDatabaseMigrations.Migrations);
            manager.Migrate();
            return new ComicsManager(connection);
        }

        /* called when a profile is created */
        public static ComicsManager InitializeComicsManager(SqliteDatabaseConnection connection) {
            var manager = new SqliteManagedDatabaseConnection(connection, ComicsDatabaseMigrations.Migrations);
            manager.Initialize();
            return new ComicsManager(connection);
        }

        /* After loading comics from disk, you may want to query known metadata from the database */
        public async Task AssignKnownMetadata(IEnumerable<Comic> comics) {
            foreach (var comic in comics) {
                var metadata = await this.TryGetMetadataAsync(comic);
                if (metadata != null) {
                    comic.Metadata = metadata;
                }
            }
        }
    }
}
