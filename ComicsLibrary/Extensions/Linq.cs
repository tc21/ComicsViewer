using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace ComicsLibrary.Extensions.Linq {
    public static class LinqExtensions {
        public static V GetValueOrDefault<K, V>(this IDictionary<K, V> dict, K key, V d = default) {
            if (dict.TryGetValue(key, out var value)) {
                return value;
            } else {
                return d;
            }
        }
    }
}
