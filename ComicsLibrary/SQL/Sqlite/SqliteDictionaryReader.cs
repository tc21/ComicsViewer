using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ComicsLibrary.SQL.Sqlite {
    public class SqliteDictionaryReader : IReadOnlyDictionary<string, object>, IDisposable {
        private readonly SqliteCommand owner;

        protected readonly SqliteDataReader reader;
        protected readonly Dictionary<string, int> keys = new Dictionary<string, int>();

        protected SqliteDictionaryReader(SqliteDataReader reader, IEnumerable<string> keys, SqliteCommand owner) {
            this.owner = owner;

            var currentIndex = 0;
            this.keys = keys.ToDictionary(key => key, key => currentIndex++);
            this.reader = reader;
        }

        public bool IsClosed => this.reader.IsClosed;
        public bool HasRows => this.reader.HasRows;
        public int FieldCount => this.reader.FieldCount;

        public bool Read() => this.reader.Read();
        public Task<bool> ReadAsync() => this.reader.ReadAsync();
        public void Close() => this.reader.Close();

        public static Task<SqliteDictionaryReader> ExecuteSelectAsync(
            SqliteDatabaseConnection connection, string table, IEnumerable<string> keys, string? restOfQuery = null,
             IDictionary<string, object>? parameters = null
        ) {
            restOfQuery ??= "";
            return ExecuteReaderAsync(connection, $"SELECT {string.Join(", ", keys)} FROM {table} {restOfQuery}", keys, parameters);
        }

        public static async Task<SqliteDictionaryReader> ExecuteReaderAsync(
            SqliteDatabaseConnection connection, string commandText, IEnumerable<string> keys,
             IDictionary<string, object>? parameters = null
        ) {
            var command = connection.CreateCommand(commandText, parameters);
            var reader = await command.ExecuteReaderAsync();
            return new SqliteDictionaryReader(reader, keys, command);
        }
        private T? GetValueOrNull<T>(string key, Func<int, T> getExistingValue) where T : struct {
            var col = this.keys[key];
            if (this.reader.IsDBNull(col)) {
                return null;
            }

            return getExistingValue(col);
        }

        private T? GetObjectOrNull<T>(string key, Func<int, T> getExistingValue) where T : class {
            var col = this.keys[key];
            if (this.reader.IsDBNull(col)) {
                return null;
            }

            return getExistingValue(col);
        }

        /* we're only supporting SQLite's native data types + int32 and bool */
        public object GetValue(string key) => this.reader.GetValue(this.keys[key]);
        public bool GetBoolean(string key) => this.reader.GetBoolean(this.keys[key]);
        public int GetInt32(string key) => this.reader.GetInt32(this.keys[key]);
        public long GetInt64(string key) => this.reader.GetInt64(this.keys[key]);
        public double GetDouble(string key) => this.reader.GetDouble(this.keys[key]);
        public string GetString(string key) => this.reader.GetString(this.keys[key]);

        public object? GetValueOrNull(string key) => this.GetObjectOrNull(key, this.reader.GetValue);
        public bool? GetBooleanOrNull(string key) => this.GetValueOrNull(key, this.reader.GetBoolean);
        public int? GetInt32OrNull(string key) => this.GetValueOrNull(key, this.reader.GetInt32);
        public long? GetInt64OrNull(string key) => this.GetValueOrNull(key, this.reader.GetInt64);
        public double? GetDoubleOrNull(string key) => this.GetValueOrNull(key, this.reader.GetDouble);
        public string? GetStringOrNull(string key) => this.GetObjectOrNull(key, this.reader.GetString);

        /* implementation of IDictionary */
        public IEnumerable<string> Keys => this.keys.Keys;
        public IEnumerable<object> Values => this.keys.Select((_, index) => this.reader.GetValue(index));
        public int Count => this.keys.Count;

        public object this[string key] => this.GetValue(key);

        public bool ContainsKey(string key) => this.keys.ContainsKey(key);

        public bool TryGetValue(string key, out object value) {
            if (!this.ContainsKey(key)) {
                // looks like we will have to do this for now
                value = new object();
                return false;
            }

            value = this[key];
            return true;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            => this.Keys.Select((key, index) => new KeyValuePair<string, object>(key, this[key])).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public void Dispose() {
            this.reader.Dispose();
            this.owner.Dispose();
        }
    }
}
