#nullable enable

using System;
using System.Collections.Generic;
using ComicsLibrary.Collections;

namespace ComicsLibrary {
    public class Playlist : ComicView, IComicProperty {
        public string Name { get; }

        private readonly ComicView parent;
        private readonly HashSet<string> uniqueIds = new HashSet<string>();

        public Playlist(ComicView parent, string name, IEnumerable<string> uniqueIds) : base(parent) {
            this.parent = parent;
            this.uniqueIds = new HashSet<string>(uniqueIds);
            this.Name = name;
        }

        public ComicView Comics => this.parent.Filtered(comic => this.uniqueIds.Contains(comic.UniqueIdentifier));

        IEnumerable<Comic> IComicProperty.Comics => this.Comics;

        public override int Count() {
            return this.Comics.Count();
        }

        public override IEnumerator<Comic> GetEnumerator() {
            return this.Comics.GetEnumerator();
        }

        public override bool Contains(string uniqueIdentifier) {
            if (!this.uniqueIds.Contains(uniqueIdentifier)) {
                return false;
            }

            if (!this.parent.Contains(uniqueIdentifier)) {
                _ = this.uniqueIds.Remove(uniqueIdentifier);
                return false;
            }

            return true;
        }

        public override Comic GetStored(string uniqueIdentifier) {
            if (!this.Contains(uniqueIdentifier)) {
                throw new ArgumentException("comic doesn't exist in this collection");
            }

            return this.parent.GetStored(uniqueIdentifier);
        }
    }
}
