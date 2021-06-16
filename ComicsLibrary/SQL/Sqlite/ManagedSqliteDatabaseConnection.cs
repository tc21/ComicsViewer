using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ComicsLibrary.SQL.Sqlite {
    public class ManagedSqliteDatabaseConnection {
        /* We store migrations as a list, where migrations[i] refers the the i'th migration, migrating from version i to i+1.
         * For example, migrations[0] is the init script, which "migrates the database from version 0 to 1". */
        private readonly List<SqliteDatabaseMigration> migrations = new();
        private readonly SqliteDatabaseConnection connection;

        public int Version => this.migrations.Count;

        public ManagedSqliteDatabaseConnection(SqliteDatabaseConnection connection, IEnumerable<SqliteDatabaseMigration> migrations) {
            this.connection = connection;

            var migrationTracker = new Dictionary<int, SqliteDatabaseMigration>();

            foreach (var migration in migrations) {
                if (migrationTracker.ContainsKey(migration.Version)) {
                    throw new ArgumentException("Cannot contain duplicate migrations");
                }

                migrationTracker[migration.Version] = migration;
            }

            for (var i = 1; i <= migrationTracker.Count; i++) {
                if (!migrationTracker.ContainsKey(i)) {
                    throw new ArgumentException($"Missing migration #{i}. Migrations must be in order starting from 1.");
                }

                this.migrations.Add(migrationTracker[i]);
            }
        } 

        private int ConnectionVersion {
            get {
                // this is the only place that isn't async
                using var command = this.connection.CreateCommand("SELECT version from __version");
                var result = command.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        public async Task InitializeAsync() {
            _ = await this.connection.ExecuteNonQueryAsync(@"
                   CREATE TABLE __version (version INTEGER NOT NULL);
                    INSERT INTO __version (version) VALUES (0);
            ");
            await this.migrations[0].MigrateAsync(this.connection);
            if (this.ConnectionVersion != 1) {
                throw new Exception("Initialization failed for unknown reasons");
            }

            await this.MigrateAsync();
        }

        public async Task MigrateAsync() {
            if (this.ConnectionVersion > this.Version) {
                throw new Exception("The database is newer than the tracked version! Are you sure this is the right database?");
            }

            for (var i = this.ConnectionVersion; i < this.Version; i++) {
                var migration = this.migrations[i];
                // The following checks should not be necessary if the program is written correctly, but just in case
                if (migration.Version != this.ConnectionVersion + 1) {
                    throw new Exception($"Invalid migration version! expected {this.ConnectionVersion + 1}, actually {migration.Version}");
                }

                await migration.MigrateAsync(this.connection);

                if (migration.Version != this.ConnectionVersion) {
                    throw new Exception("Migration failed for unknown reasons");
                }
            }
        }
    }
}
