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

        /* if a comic already exists, it updates it with the given data; otherwise a new row is added */
        /* explanation; basically the way we implemented this the databaseconnection doesn't differentiate between
         * active (visible to viewmodel) and inactive (invisible to viewmodel, but still exists) comics. I *could* 
         * make it do so, but the program doesn't have such a need yet */
        public async Task AddOrUpdateComicsAsync(IEnumerable<Comic> comics) {
            // This weird trick protects your database integrity with just two lines of code! 
            // Enterprise coders hate him!
            using var transaction = this.Connection.BeginTransaction();

            foreach (var comic in comics) {
                if (await this.Connection.HasComicAsync(comic)) {
                    await this.Connection.UpdateComicAsync(comic);
                } else {
                    _ = await this.Connection.AddComicAsync(comic);
                }
            }

            transaction.Commit();
        }

        public async Task RemoveComicsAsync(IEnumerable<Comic> comics) {
            using var transaction = this.Connection.BeginTransaction();

            foreach (var comic in comics) {
                await this.Connection.InvalidateComicAsync(comic);
            }

            transaction.Commit();
        }

        /* After loading comics from disk, you may want to query known metadata from the database */
        public async Task<List<Comic>> RetrieveKnownMetadataAsync(IEnumerable<Comic> comics) {
            var result = new List<Comic>();

            foreach (var comic in comics) {
                var metadata = await this.TryGetMetadataAsync(comic);
                if (metadata is ComicMetadata m) {
                    result.Add(comic.With(metadata: m));
                } else {
                    result.Add(comic);
                }
            }

            return result;
        }
    }
}
