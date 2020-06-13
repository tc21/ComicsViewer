using System;
using System.Collections.Generic;
using System.Text;

namespace ComicsLibrary.SQL.Migrations {
    internal class Migration_Initialize : ComicsDatabaseMigration {
        public Migration_Initialize() : base(
            version: 1,
            sql: @"
CREATE TABLE comics (
    folder TEXT NOT NULL,

    unique_name TEXT UNIQUE NOT NULL,

    title TEXT NOT NULL,
    author TEXT NOT NULL,
    category TEXT NOT NULL,

    display_title TEXT,
    display_author TEXT,
    display_category TEXT,

    thumbnail_source TEXT,

    date_added TEXT NOT NULL DEFAULT current_timestamp,

    loved INTEGER CHECK (loved IN (0, 1)),
    disliked INTEGER CHECK (loved IN (0, 1)),

    active INTEGER DEFAULT 1 CHECK (active IN (0, 1))
);

CREATE TABLE tags (
    name TEXT NOT NULL UNIQUE ON CONFLICT IGNORE
);

CREATE TABLE comic_tags (
    comicid INTEGER NOT NULL,
    tagid INTEGER NOT NULL,
    UNIQUE(comicid, tagid) ON CONFLICT IGNORE
);

CREATE TABLE progress (
    comicid INTEGER NOT NULL,
    progress INTEGER NOT NULL,
    
    UNIQUE(comicid) ON CONFLICT REPLACE
);
"
        ) { }
    }
}
