using ComicsLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.Support {
    /* Shadows over the (former) list at MainViewModel.comics to provide set-like behavior */
    class ComicList : ICollection<Comic> {
        private readonly Dictionary<string, Comic> values = new Dictionary<string, Comic>();

        public ComicList() { }
        public ComicList(IEnumerable<Comic> comics) {
            this.values = comics.ToDictionary(comic => comic.UniqueIdentifier);
        }

        /* adding an existing item does nothing. In the future we may want to update this. Reloading existing items from
         * disk may be meaningful if the location of the item changed without the author- and title-level folder names. */
        public bool Add(Comic comic) {
            if (this.Contains(comic)) {
                return false;
            } 

            this.values.Add(comic.UniqueIdentifier, comic);
            return true;
        }

        public void CopyTo(Comic[] array, int arrayIndex)  {
            foreach (var comic in this) {
                array[arrayIndex++] = comic;
            }
        }

        public int Count => this.values.Count();
        public bool IsReadOnly => false;
        public void Clear() => this.values.Clear();
        public bool Contains(Comic comic) => this.values.ContainsKey(comic.UniqueIdentifier);
        public IEnumerator<Comic> GetEnumerator() => this.values.Values.GetEnumerator();
        public bool Remove(Comic comic) => this.values.Remove(comic.UniqueIdentifier);
        public bool Remove(Comic comic, out Comic retrieved) => this.values.Remove(comic.UniqueIdentifier, out retrieved);
        IEnumerator IEnumerable.GetEnumerator() => this.values.Values.GetEnumerator();
        void ICollection<Comic>.Add(Comic comic) => this.Add(comic);
    }
}
