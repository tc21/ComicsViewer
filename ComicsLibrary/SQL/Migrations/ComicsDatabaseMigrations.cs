using System.Collections.Generic;

namespace ComicsLibrary.SQL.Migrations {
    internal static class ComicsDatabaseMigrations {
        public static readonly List<ComicsDatabaseMigration> Migrations = new List<ComicsDatabaseMigration> {
            // An initializes the database. We could just use instances of ComicsDatabaseMigration, but this allows us
            // to give them a descriptive name and keep them in separate files
            new Migration_Initialize(),
            // These three migrations are because we want to continue using the Comics.WPF databases, which are already on version 4
            new Migration_Stub(2),
            new Migration_Stub(3),
            new Migration_Stub(4),
            // We also verify that the migration system works with an otherwise useless migration
            new Migration_5(),
            // Playlists
            new Migration_6_Playlists(),
            // Primary keys
            new Migration_7_PrimaryKeys(),
            // Insert new migrations here
        };
    }
}
