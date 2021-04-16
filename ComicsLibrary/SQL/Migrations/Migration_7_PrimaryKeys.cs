namespace ComicsLibrary.SQL.Migrations {
    // This migration ensures explicit primary keys exist on all tables, so that we can move away from 
    // messing around with implicit rowids, as well as ensure compatibility with external tools (specifically diesel)
    internal class Migration_7_PrimaryKeys : ComicsDatabaseMigration {
        public Migration_7_PrimaryKeys() : base(
            version: 7,
            sql: @"
-- setup --

CREATE TABLE comic_tags_temp (
    comic TEXT NOT NULL,
    tag TEXT NOT NULL
);

CREATE TABLE playlist_items_temp (
    playlist TEXT NOT NULL,
    comic TEXT NOT NULL
);

INSERT INTO comic_tags_temp
    SELECT c.unique_name, t.name
    FROM comic_tags ct
    INNER JOIN comics c ON ct.comicid = c.rowid
    INNER JOIN tags t ON ct.tagid = t.rowid;

INSERT INTO playlist_items_temp
    SELECT p.name, c.unique_name
    FROM playlist_items pi
    INNER JOIN playlists p ON pi.playlistid = p.rowid
    INNER JOIN comics c ON pi.comicid = c.rowid;

-- comics -- 

CREATE TABLE comics_new (
    path TEXT NOT NULL,
    unique_identifier TEXT NOT NULL PRIMARY KEY ON CONFLICT REPLACE,
    title TEXT NOT NULL,
    author TEXT NOT NULL,
    category TEXT NOT NULL,
    display_title TEXT,
    thumbnail_source TEXT,
    date_added TEXT NOT NULL DEFAULT current_timestamp,
    loved INTEGER DEFAULT 0 CHECK (loved IN (0, 1)),
    disliked INTEGER DEFAULT 0 CHECK (disliked IN (0, 1)),
    active INTEGER DEFAULT 1 CHECK (active IN (0, 1))
);

INSERT INTO comics_new
    SELECT folder, unique_name, title, author, category, display_title, thumbnail_source, date_added, loved, disliked, active
    FROM comics;

DROP TABLE comics;
ALTER TABLE comics_new RENAME TO comics;

-- tags --

CREATE TABLE tags_new (
    name TEXT NOT NULL PRIMARY KEY ON CONFLICT IGNORE
);

INSERT INTO tags_new SELECT DISTINCT name FROM tags;

DROP TABLE tags;
ALTER TABLE tags_new RENAME TO tags;

CREATE TABLE comic_tags_new (
    comic TEXT NOT NULL REFERENCES comics(unique_identifier) ON DELETE CASCADE,
    tag TEXT NOT NULL REFERENCES tags(name) ON DELETE CASCADE,

    PRIMARY KEY(comic, tag) ON CONFLICT IGNORE
);

INSERT INTO comic_tags_new SELECT DISTINCT comic, tag FROM comic_tags_temp;

DROP TABLE comic_tags;
DROP TABLE comic_tags_temp;
ALTER TABLE comic_tags_new RENAME TO comic_tags;

-- playlists --

CREATE TABLE playlists_new (
    name TEXT NOT NULL PRIMARY KEY ON CONFLICT IGNORE
);

INSERT INTO playlists_new SELECT DISTINCT name FROM playlists;

DROP TABLE playlists;
ALTER TABLE playlists_new RENAME TO playlists;

CREATE TABLE playlist_items_new (
    playlist TEXT NOT NULL REFERENCES playlists(name) ON DELETE CASCADE,
    comic TEXT NOT NULL REFERENCES comics(unique_identifier) ON DELETE CASCADE,

    PRIMARY KEY(playlist, comic) ON CONFLICT IGNORE
);

INSERT INTO playlist_items_new SELECT DISTINCT playlist, comic FROM playlist_items_temp;

DROP TABLE playlist_items;
DROP TABLE playlist_items_temp;
ALTER TABLE playlist_items_new RENAME TO playlist_items;

-- progress --

DROP TABLE progress;
"
        ) { }
    }
}
