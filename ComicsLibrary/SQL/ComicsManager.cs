﻿using System.Collections.Generic;
using System.Threading.Tasks;
using ComicsLibrary.Collections;
using ComicsLibrary.SQL.Migrations;
using ComicsLibrary.SQL.Sqlite;

namespace ComicsLibrary.SQL {
    public class ComicsManager {
        public ComicsDatabaseConnection Connection { get; }

        public ComicsManager(SqliteDatabaseConnection databaseConnection) {
            this.Connection = new ComicsDatabaseConnection(databaseConnection);
        }

        public async Task<IEnumerable<Comic>> GetAllComicsAsync() {
            return await this.Connection.GetActiveComicsAsync();
        }

        private Task<ComicMetadata?> TryGetMetadataAsync(Comic comic) {
            return this.Connection.TryGetComicMetadataAsync(comic);
        }

        /* called when a profile is first loaded */
        public static async Task<ComicsManager> MigratedComicsManagerAsync(SqliteDatabaseConnection connection) {
            var manager = new ManagedSqliteDatabaseConnection(connection, ComicsDatabaseMigrations.Migrations);
            await manager.MigrateAsync();
            return new ComicsManager(connection);
        }

        /* called when a profile is created */
        public static async Task<ComicsManager> InitializeComicsManagerAsync(SqliteDatabaseConnection connection) {
            var manager = new ManagedSqliteDatabaseConnection(connection, ComicsDatabaseMigrations.Migrations);
            await manager.InitializeAsync();
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
                    await this.Connection.AddComicAsync(comic);
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
                if (metadata is { } m) {
                    result.Add(comic.With(metadata: m));
                } else {
                    result.Add(comic);
                }
            }

            return result;
        }

        public Task<List<Playlist>> GetPlaylistsAsync(ComicList comics) {
            return this.Connection.GetAllPlaylistsAsync(comics);
        }

        public Task RemovePlaylistAsync(string name) {
            return this.Connection.RemovePlaylistAsync(name);
        }

        public Task AddPlaylistAsync(string name) {
            return this.Connection.AddPlaylistAsync(name);
        }

        public Task RenamePlaylistAsync(string oldName, string newName) {
            return this.Connection.RenamePlaylistAsync(oldName, newName);
        }

        public async Task AddComicsToPlaylistAsync(string playlist, IEnumerable<Comic> comics) {
            using var transaction = this.Connection.BeginTransaction();

            foreach (var comic in comics) {
                await this.Connection.AssociateComicWithPlaylistAsync(playlist, comic);
            }

            transaction.Commit();
        }

        public async Task RemoveComicsFromPlaylistAsync(string playlist, IEnumerable<Comic> comics) {
            using var transaction = this.Connection.BeginTransaction();

            foreach (var comic in comics) {
                await this.Connection.UnassociateComicWithPlaylistAsync(playlist, comic);
            }

            transaction.Commit();
        }
    }
}
