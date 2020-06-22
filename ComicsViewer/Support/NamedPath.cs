#nullable enable 

namespace ComicsViewer.Support {
    public class NamedPath {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";

        public override bool Equals(object obj) {
            if (!(obj is NamedPath other)) {
                return false;
            }

            return this.Name.Equals(other.Name) && this.Path.Equals(other.Path);
        }

        public override int GetHashCode() {
            return this.Name.GetHashCode() ^ this.Path.GetHashCode();
        }
    }
}