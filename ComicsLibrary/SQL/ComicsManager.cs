using ComicsLibrary.SQL.Migrations;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using TC.Database;
using TC.Database.MicrosoftSqlite;
using SqliteManagedDatabaseConnection = TC.Database.ManagedDatabaseConnection<
    TC.Database.MicrosoftSqlite.SqliteDatabaseConnection, Microsoft.Data.Sqlite.SqliteConnection, 
    Microsoft.Data.Sqlite.SqliteCommand, Microsoft.Data.Sqlite.SqliteDataReader>;

namespace ComicsLibrary.SQL {
    public class ComicsManager : ComicsReadOnlyManager {
        internal ComicsManager(SqliteDatabaseConnection databaseConnection) : base(databaseConnection) { }

        public static ComicsManager MigratedComicsManager(SqliteDatabaseConnection connection) {
            var manager = new SqliteManagedDatabaseConnection(connection, ComicsDatabaseMigrations.Migrations);
            manager.Migrate();
            return new ComicsManager(connection);
        }
    }
}
