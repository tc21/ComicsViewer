namespace ComicsLibrary.SQL.Migrations {
    internal class Migration_6_Playlists : ComicsDatabaseMigration {
        public Migration_6_Playlists() : base(
            version: 6,
            sql: @"
CREATE TABLE playlists (
    name TEXT NOT NULL
);

CREATE TABLE playlist_items (
    playlistid INT NOT NULL,
    comicid INT NOT NULL
);
",
            beforeMigrate: manager => { },
            afterMigrate: manager => { }
        ) { }
    }
}
