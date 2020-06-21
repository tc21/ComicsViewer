using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.Support {
    class DefaultDictionary<TKey, TValue> : IDictionary<TKey, TValue> {
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

        #region Auto-generated IDictionary implementation 


        public ICollection<TKey> Keys => ((IDictionary<TKey, TValue>)this.dictionary).Keys;
        public ICollection<TValue> Values => ((IDictionary<TKey, TValue>)this.dictionary).Values;
        public int Count => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).Count;
        public bool IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).IsReadOnly;
        public void Add(TKey key, TValue value) => ((IDictionary<TKey, TValue>)this.dictionary).Add(key, value);
        public void Add(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).Add(item);
        public void Clear() => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).Clear();
        public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).Contains(item);
        public bool ContainsKey(TKey key) => ((IDictionary<TKey, TValue>)this.dictionary).ContainsKey(key);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)this.dictionary).GetEnumerator();
        public bool Remove(TKey key) => ((IDictionary<TKey, TValue>)this.dictionary).Remove(key);
        public bool Remove(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)this.dictionary).Remove(item);
        public bool TryGetValue(TKey key, out TValue value) => ((IDictionary<TKey, TValue>)this.dictionary).TryGetValue(key, out value);
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.dictionary).GetEnumerator();

        #endregion
    }
}
