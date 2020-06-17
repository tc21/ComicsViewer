using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TC.Database.MicrosoftSqlite;

namespace ComicsLibrary.SQL {
    /* This class houses extensions to SqliteDatabaseConnection that are planned to be moved to TCSupport, 
     * but are still in testing */
    internal static class SqliteDatabaseConnection_Extensions {
        /* returns the newly inserted rowid, or null if the insert failed or did not modify the database */
        public static async Task<int?> ExecuteInsertAsync(this SqliteDatabaseConnection connection, string tableName, IDictionary<string, object> initialValues) {
            var queryString = $@"
                INSERT INTO {tableName} (
                    {string.Join(", ", initialValues.Keys)}
                ) VALUES (
                    {string.Join(", ", initialValues.Keys.Select(key => $"@{key}"))}
                )";

            var parameters = initialValues.ToDictionary(pair => $"@{pair.Key}", pair => pair.Value);

            var result = await connection.ExecuteNonQueryAsync(queryString, parameters);

            if (result == 0) {
                return null;
            }

            return await connection.ExecuteScalarAsync<int>("SELECT last_insert_rowid()");
        }
    }
}
