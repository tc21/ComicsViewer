using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.Support {
    public class FilterViewAuxiliaryInfo {
        public IList<CountedString> Categories { get; }
        public IList<CountedString> Authors { get; }
        public IList<CountedString> Tags { get; }

        public FilterViewAuxiliaryInfo(IDictionary<string, int> categories, IDictionary<string, int> authors, IDictionary<string, int> tags) {
            this.Categories = categories.Select(pair => new CountedString(pair.Key, pair.Value)).ToList();
            this.Authors = authors.Select(pair => new CountedString(pair.Key, pair.Value)).ToList();
            this.Tags = tags.Select(pair => new CountedString(pair.Key, pair.Value)).ToList();
        }
    }
}
