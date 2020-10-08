using System;
using System.Collections;
using System.Collections.Generic;

namespace ComicsViewer.Support {
    internal class DefaultDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue> {
        private readonly Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
        private readonly Func<TValue> makeDefault;

        public DefaultDictionary() : this(() => default) { }

        public DefaultDictionary(Func<TValue> makeDefault) {
            this.makeDefault = makeDefault;
        }

        public TValue this[TKey key] { 
            get {
                if (!this.dictionary.ContainsKey(key)) {
                    this.dictionary[key] = this.makeDefault();
                }

                return this.dictionary[key];
            }
            set => this.dictionary[key] = value; 
        }

        public bool IsReadOnly => false;
        public bool Contains(KeyValuePair<TKey, TValue> item) => this[item.Key].Equals(item.Value);
        public bool ContainsKey(TKey key) => true;

        public bool TryGetValue(TKey key, out TValue value) {
            value = this[key];
            return true;
        }

        #region Auto-generated ICollection implementation 

        public int Count => this.dictionary.Count;
        public ICollection<TKey> Keys => this.dictionary.Keys;
        public ICollection<TValue> Values => this.dictionary.Values;
        public bool Remove(TKey key) => this.dictionary.Remove(key);
        public void Add(TKey key, TValue value) => this.dictionary.Add(key, value);
        public void Clear() => this.dictionary.Clear();
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => this.dictionary.GetEnumerator();

        public void Add(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).Add(item);
        public bool Remove(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).Remove(item);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).CopyTo(array, arrayIndex);

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => this.Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => this.Values;
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        #endregion
    }
}
