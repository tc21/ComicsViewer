#nullable enable

using System;
using System.Collections.Generic;
using ComicsLibrary;

namespace ComicsViewer.ViewModels {
    public class DragAndDropShortcutItem {
        public string Name { get; }
        public Action<IEnumerable<Comic>> OnDrop { get; }

        public DragAndDropShortcutItem(string name, Action<IEnumerable<Comic>> onDrop) {
            this.OnDrop = onDrop;
            this.Name = name;
        }
    }
}