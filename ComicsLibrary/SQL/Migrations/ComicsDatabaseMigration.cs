using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using TC.Database;
using TC.Database.MicrosoftSqlite;

namespace ComicsLibrary.SQL {
    internal class ComicsDatabaseMigration : DatabaseMigration<SqliteDatabaseConnection, SqliteConnection, SqliteCommand, SqliteDataReader> {
        public ComicsDatabaseMigration
            (int version, string? sql, Action<ComicsManager>? beforeMigrate = null, Action<ComicsManager>? afterMigrate = null)
          : base(version, sql, conn => beforeMigrate?.Invoke(new ComicsManager(conn)), conn => afterMigrate?.Invoke(new ComicsManager(conn))) { }
    }
}
