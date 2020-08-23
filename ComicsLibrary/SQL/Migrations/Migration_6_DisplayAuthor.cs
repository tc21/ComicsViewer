using System;

namespace ComicsLibrary.SQL.Migrations {
    internal class Migration_6_DisplayAuthor : ComicsDatabaseMigration {
        public Migration_6_DisplayAuthor() : base(
            version: 6,
            sql: @"
CREATE TABLE author_aliases (
    name TEXT UNIQUE NOT NULL,
    alias TEXT NOT NULL,
);

CREATE TABLE category_aliases (
    name TEXT UNIQUE NOT NULL,
    alias TEXT NOT NULL
);
",
            beforeMigrate: manager => _ = manager.Connection == null ? throw new Exception() : 0,
            afterMigrate: manager => _ = manager.Connection == null ? throw new Exception() : 0
        ) { }
    }
}
