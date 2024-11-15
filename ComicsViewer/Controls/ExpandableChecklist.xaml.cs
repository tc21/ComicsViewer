﻿using System;
using System.Collections.Generic;
using System.Linq;
using ComicsViewer.Common;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

#nullable enable

namespace ComicsViewer.Controls {
    public sealed partial class ExpandableChecklist {
        public ExpandableChecklist() {
            this.InitializeComponent();

            this.SelectedItemsChanged += this.ExpandableChecklist_SelectedItemsChanged;

            _ = VisualStateManager.GoToState(this, "Collapsed", false);
        }

        private List<CountedStringCheckBoxItem> itemListItemsSource = new();
        /* We can generalize this control and let the user provide a DataTemplate and an ICollection of arbitrary type,
         * but let's not build anything that I wouldn't end up needing. 
         * Note that we use the implementation detail that duplicate CountedString.Names don't exist */
        public IList<CountedString>? ItemsSource {
            get => this.GetValue(ItemsSourceProperty) as IList<CountedString>;
            // Note: As it is currently implemented, ItemsSource is only supposed to be set once.
            set {
                this.SetValue(ItemsSourceProperty, value);
                // We allow setting selectedItems before ItemsSource, so we check for it here
                if (value == null) {
                    this.ItemList.ItemsSource = null;
                    return;
                }

                this.itemListItemsSource = value.Select(e => new CountedStringCheckBoxItem(e, this._selectedItems.Contains(e))).ToList();
                this.ItemList.ItemsSource = this.itemListItemsSource;
            }
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IList<CountedString>), typeof(ExpandableChecklist), new PropertyMetadata(null));

        public bool IsExpanded {
            get => this.HeaderToggleButton.IsChecked ?? false;
            set => this.HeaderToggleButton.IsChecked = value;
        }

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ExpandableChecklist), new PropertyMetadata(false));

        public string Header {
            get => (string)this.GetValue(HeaderProperty);
            set => this.SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(ExpandableChecklist), new PropertyMetadata(""));

        private HashSet<CountedString> _selectedItems = new();
        public IEnumerable<CountedString> SelectedItems {
            set {
                var oldSelectedItems = this._selectedItems;
                this._selectedItems = new HashSet<CountedString>(value);

                foreach (var item in this.itemListItemsSource) {
                    item.IsChecked = this._selectedItems.Contains(item.Item);
                }

                var addedItems = this._selectedItems.Where(item => !oldSelectedItems.Contains(item)).ToList();

                // We use this set in place since we don't need it anymore
                _ = oldSelectedItems.RemoveWhere(this._selectedItems.Contains);
                this.SelectedItemsChanged(this, new SelectedItemsChangedEventArgs(removedItems: oldSelectedItems, addedItems));
            }
        }

        private void DeselectAllButton_Tapped(object sender, TappedRoutedEventArgs e) {
            var removedItems = this._selectedItems.ToList();
            this._selectedItems.Clear();

            foreach (var item in this.itemListItemsSource) {
                item.IsChecked = this._selectedItems.Contains(item.Item);
            }

            this.SelectedItemsChanged(this, new SelectedItemsChangedEventArgs(removedItems, EmptyList));
        }

        private void ChecklistItem_Unchecked(object sender, RoutedEventArgs e) {
            if (((FrameworkElement)sender).DataContext is not CountedStringCheckBoxItem item) {
                throw new ProgrammerError("ChecklistItem_Unchecked received unexpected item");
            }

            _ = this._selectedItems.Remove(item.Item);
            this.SelectedItemsChanged(this, new SelectedItemsChangedEventArgs(
                new List<CountedString> { item.Item }, EmptyList));
        }

        private void ChecklistItem_Checked(object sender, RoutedEventArgs e) {
            if (((FrameworkElement)sender).DataContext is not CountedStringCheckBoxItem item) {
                throw new ProgrammerError("ChecklistItem_Checked received unexpected item");
            }

            _ = this._selectedItems.Add(item.Item);
            this.SelectedItemsChanged(this, new SelectedItemsChangedEventArgs(
                EmptyList, new List<CountedString> { item.Item }));
        }

        private void ExpandableChecklist_SelectedItemsChanged(ExpandableChecklist sender, SelectedItemsChangedEventArgs e) {
            if (this._selectedItems.Count == 0) {
                this.DeselectAllButton.Visibility = Visibility.Collapsed;
                this.SelectedItemsIndicator.Visibility = Visibility.Collapsed;
            } else {
                this.SelectedItemsCounter.Text = this._selectedItems.Count.ToString();
                this.DeselectAllButton.Visibility = Visibility.Visible;
                this.SelectedItemsIndicator.Visibility = Visibility.Visible;
            }
        }

        public delegate void SelectedItemsChangedEventHandler(ExpandableChecklist sender, SelectedItemsChangedEventArgs e);
        public event SelectedItemsChangedEventHandler SelectedItemsChanged = delegate { };

        public event EventHandler Expanding = delegate { };

        private static readonly IReadOnlyList<CountedString> EmptyList = new List<CountedString>().AsReadOnly();

        /* For some reason, when the pointer leaves the ScrollViewer for the first time in its lifecycle, if fires a
         * DataContextChanged(NewValue = null) event, causing the item list to be reset, unchecking any items the user
         * checked during that time. This prevents that from happening. */
        private void ScrollViewer_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) {
            args.Handled = true;
        }

        private void HeaderToggleButton_Checked(object sender, RoutedEventArgs e) {
            _ = VisualStateManager.GoToState(this, "Expanded", true);
            this.Expanding(this, EventArgs.Empty);
        }

        private void HeaderToggleButton_Unchecked(object sender, RoutedEventArgs e) {
            _ = VisualStateManager.GoToState(this, "Collapsed", true);
        }
    }

    public class SelectedItemsChangedEventArgs {
        public IEnumerable<CountedString> RemovedItems { get; }
        public IEnumerable<CountedString> AddedItems { get; }

        public SelectedItemsChangedEventArgs(IEnumerable<CountedString> removedItems, IEnumerable<CountedString> addedItems) {
            this.RemovedItems = removedItems;
            this.AddedItems = addedItems;
        }
    }

    public class CountedStringCheckBoxItem : CheckBoxItem<CountedString> {
        public CountedStringCheckBoxItem(CountedString cs, bool isChecked = false) : base(cs, isChecked) { }
    }
}
