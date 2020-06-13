using System;
using System.Collections.Generic;
using System.Text;

namespace ComicsLibrary.SQL.Migrations {
    internal class Migration_5 : ComicsDatabaseMigration {
        public Migration_5() : base(
            version: 5,
            sql: @"
CREATE TABLE __application_name (
    name TEXT NOT NULL
);

INSERT INTO __application_name (name)
    VALUES ('ComicsViewer (UWP)');
",
            beforeMigrate: manager => _ = manager.Connection == null ? throw new Exception() : 0,
            afterMigrate: manager => _ = manager.Connection == null ? throw new Exception() : 0
        ) { }
    }
}
