#nullable enable

namespace ComicsViewer.Support {
    public readonly struct CountedString {
        public string Name { get; }
        public int Count { get; }

        public CountedString(string name, int count) {
            this.Name = name;
            this.Count = count;
        }
    }
}
