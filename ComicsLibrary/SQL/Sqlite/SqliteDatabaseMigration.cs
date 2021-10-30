using System;
using System.Threading.Tasks;

namespace ComicsLibrary.SQL.Sqlite {
    public class SqliteDatabaseMigration {
        public readonly int Version;
        private readonly string? command;
        private readonly Action<SqliteDatabaseConnection>? beforeMigrate;
        private readonly Action<SqliteDatabaseConnection>? afterMigrate;

        protected SqliteDatabaseMigration(
            int version, string? command,
            Action<SqliteDatabaseConnection>? beforeMigrate = null,
            Action<SqliteDatabaseConnection>? afterMigrate = null
        ) {
            this.Version = version;
            this.command = command;
            this.beforeMigrate = beforeMigrate;
            this.afterMigrate = afterMigrate;
        }

        public async Task MigrateAsync(SqliteDatabaseConnection connection) {
            using var transaction = connection.Connection.BeginTransaction();
            // With the transaction, any exceptions will be thrown as normal, but changes will not commit unless
            // the function successfully runs till the end.

            this.beforeMigrate?.Invoke(connection);
            if (this.command != null) {
                _ = await connection.ExecuteNonQueryAsync(this.command);
            }
            _ = await connection.ExecuteNonQueryAsync($"UPDATE __version SET version = {this.Version}");
            this.afterMigrate?.Invoke(connection);

            transaction.Commit();
        }
    }
}
