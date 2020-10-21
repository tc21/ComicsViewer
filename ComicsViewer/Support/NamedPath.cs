#nullable enable 

namespace ComicsViewer.Support {
    public class NamedPath: ISelectable {
        // todo: when we adopt c#9, we should switch this over to the new record class
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