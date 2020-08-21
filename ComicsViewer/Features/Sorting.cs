using ComicsLibrary;
using ComicsLibrary.Collections;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.Features {
    public static class Sorting {
        public enum SortSelector {
            Title, Author, DateAdded, ItemCount, Random
        }

        public static readonly string[] SortSelectorNames = { "Title", "Author", "Date Added", "Item Count", "Random" };
    }
}
