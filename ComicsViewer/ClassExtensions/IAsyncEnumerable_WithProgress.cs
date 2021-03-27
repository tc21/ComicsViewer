using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace ComicsViewer.ClassExtensions {
    public static class IAsyncEnumerable_WithProgress {
        public static async IAsyncEnumerable<T> WithProgressAsync<T>(
            this IAsyncEnumerable<T> enumerable, IProgress<int> progress, [EnumeratorCancellation] CancellationToken ct = default
        ) {
            var count = 0;

            await foreach (var item in enumerable.WithCancellation(ct)) {
                ct.ThrowIfCancellationRequested();

                count += 1;
                progress.Report(count);
                yield return item;
            }
        }
    }
}
