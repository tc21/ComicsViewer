using ComicsViewer.Support;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace ComicsViewer.Features {
    /// <summary>
    /// It's important to note that modifications to NamedPaths returned from this thing will not be reflected in this collection.
    /// </summary>
    public class RootPaths : ICollection<NamedPath> {
        private readonly Dictionary<string, string> paths = new();

        public RootPaths(IEnumerable<NamedPath>? paths = null) {
            if (paths == null) {
                return;
            }

            foreach (var path in paths) {
                this.Add(path);
            }
        }

        public int Count => this.paths.Count;
        public bool IsReadOnly => false;

        public void Add(string name, string path) => this.paths.Add(name, path);
        public bool ContainsName(string name) => this.paths.ContainsKey(name);
        public bool ContainsPath(string path) => this.paths.ContainsValue(path);
        public bool Remove(string name) => this.paths.Remove(name);
        public bool TryGetValue(string name, [NotNullWhen(true)] out NamedPath? value) {
            if (this.paths.TryGetValue(name, out var path)) {
                value = new NamedPath { Name = name, Path = path };
                return true;
            }

            value = null;
            return false;
        }

        public string this[string name] {
            get => this.paths[name];
            set => this.paths[name] = value;
        }

        public void Add(NamedPath item) => this.paths.Add(item.Name, item.Path);
        public void Clear() => this.paths.Clear();
        public bool Contains(NamedPath item) => this.paths.TryGetValue(item.Name, out var path) && path == item.Path;
        public bool Remove(NamedPath item) => this.Contains(item) && this.paths.Remove(item.Name);

        public void CopyTo(NamedPath[] array, int arrayIndex) {
            foreach (var item in this) {
                array[arrayIndex++] = item;
            }
        }

        public IEnumerator<NamedPath> GetEnumerator() {
            foreach (var (key, value) in this.paths) {
                yield return new NamedPath { Name = key, Path = value };
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public class RootPathsJsonConverter : JsonConverter<RootPaths> {
        public override RootPaths Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            var paths = JsonSerializer.Deserialize<List<NamedPath>>(ref reader);
            return new RootPaths(paths);
        }

        public override void Write(Utf8JsonWriter writer, RootPaths value, JsonSerializerOptions options) {
            var paths = value.ToList();
            JsonSerializer.Serialize(writer, paths);
        }
    }
}