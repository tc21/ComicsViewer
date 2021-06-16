#nullable enable 

namespace ComicsViewer.Support {
    public record NamedPath: ISelectable {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }
}