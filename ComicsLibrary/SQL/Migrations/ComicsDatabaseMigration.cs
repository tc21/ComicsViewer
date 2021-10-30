using System;
using ComicsLibrary.SQL.Sqlite;

namespace ComicsLibrary.SQL.Migrations {
    internal class ComicsDatabaseMigration : SqliteDatabaseMigration {
        protected ComicsDatabaseMigration(
            int version, string? sql, Action<ComicsManager>? beforeMigrate = null, Action<ComicsManager>? afterMigrate = null
        ) : base(
            version,
            sql,
            conn => beforeMigrate?.Invoke(new ComicsManager(conn)),
            conn => afterMigrate?.Invoke(new ComicsManager(conn))
        ) { }
    }
}
