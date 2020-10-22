using ComicsViewer.Support;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ComicsViewer.Pages {
    public class ItemPickerDialogNavigationArguments {
        public ItemPickerDialogProperties Properties { get; }
        public Action<ISelectable> Action { get; }
        public IEnumerable<ISelectable> Items { get; }

        private ItemPickerDialogNavigationArguments(ItemPickerDialogProperties properties, IEnumerable<ISelectable> items, Action<ISelectable> action) {
            this.Properties = properties;
            this.Action = action;
            this.Items = items;
        }

        // Note: we can't use generic types in a xaml, so we do this
        public static ItemPickerDialogNavigationArguments New<T>(ItemPickerDialogProperties properties, IEnumerable<T> items, Action<T> action) where T : ISelectable {
            return new ItemPickerDialogNavigationArguments(properties, items.Cast<ISelectable>(), x => action((T)x));
        }

        public static ItemPickerDialogNavigationArguments New(ItemPickerDialogProperties properties, IEnumerable<string> items, Action<string> action) {
            return new ItemPickerDialogNavigationArguments(properties, items.Select(s => new SelectableString(s)), x => action(x.Name));
        }

        private class SelectableString : ISelectable {
            public string Name { get; }

            public SelectableString(string name) {
                this.Name = name;
            }
        }
    }

    public class ItemPickerDialogProperties {
        public string ComboBoxHeader { get; }
        public string Action { get; }
        public string? Warning { get; }
        public string ActionDescription { get; }

        public ItemPickerDialogProperties(string comboBoxHeader, string action, string actionDescription, string? warning = null) {
            this.ComboBoxHeader = comboBoxHeader;
            this.Action = action;
            this.Warning = warning;
            this.ActionDescription = actionDescription;
        }
    }
}
