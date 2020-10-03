using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ComicsLibrary.SQL.Sqlite {
    public class SqliteDatabaseConnection {
        public SqliteConnection Connection { get; }

        public SqliteDatabaseConnection(SqliteConnection connection) {
            this.Connection = connection;
        }

        public SqliteCommand CreateCommand(string commandText, IDictionary<string, object>? parameters = null) {
            var command = this.Connection.CreateCommand();
            command.CommandText = commandText;

            if (parameters != null) {
                command.Parameters.AddRange(CreateParametersOrNull(parameters));
            }

            return command;
        }

        private static IEnumerable<SqliteParameter>? CreateParametersOrNull(IDictionary<string, object>? parameters) {
            return parameters?.Select(pair => new SqliteParameter(pair.Key, pair.Value));
        }

        public Task<int> ExecuteNonQueryAsync(string commandText, IDictionary<string, object>? parameters = null) {
            using var command = this.CreateCommand(commandText, parameters);
            return command.ExecuteNonQueryAsync();
        }

        public async Task<T> ExecuteScalarAsync<T>(string commandText, IDictionary<string, object>? parameters = null) {
            using var command = this.CreateCommand(commandText, parameters);
            return MagicCast<T>(await command.ExecuteScalarAsync());
        }

        public async Task<SqliteDictionaryReader> ExecuteSelectAsync(
            string table, IEnumerable<string> keys, string? restOfQuery = null, IDictionary<string, object>? parameters = null
        ) {
            return await SqliteDictionaryReader.ExecuteSelectAsync(this, table, keys, restOfQuery, parameters);
        }

        public async Task<SqliteDictionaryReader> ExecuteReaderAsync(
            string commandText, IEnumerable<string> keys, IDictionary<string, object>? parameters = null
        ) {
            return await SqliteDictionaryReader.ExecuteReaderAsync(this, commandText, keys, parameters);
        }

        public async Task<int?> ExecuteInsertAsync(string tableName, IDictionary<string, object> initialValues) {
            var queryString = $@"
                INSERT INTO {tableName} (
                    {string.Join(", ", initialValues.Keys)}
                ) VALUES (
                    {string.Join(", ", initialValues.Keys.Select(key => $"@{key}"))}
                )";

            var parameters = initialValues.ToDictionary(pair => $"@{pair.Key}", pair => pair.Value);

            var result = await this.ExecuteNonQueryAsync(queryString, parameters);

            if (result == 0) {
                return null;
            }

            return await this.ExecuteScalarAsync<int>("SELECT last_insert_rowid()");
        }

        private static T MagicCast<T>(object o) {
            if (typeof(T) == typeof(int)) {
                return (T)(object)Convert.ToInt32(o);
            }

            return (T)o;
        }
    }
}
