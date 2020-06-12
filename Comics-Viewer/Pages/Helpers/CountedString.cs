#nullable enable

namespace ComicsViewer.Pages.Helpers {
    public struct CountedString {
        public string Name { get; }
        public int Count { get; }

        public CountedString(string name, int count) {
            this.Name = name;
            this.Count = count;
        }
    }
}
