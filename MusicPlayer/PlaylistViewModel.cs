using System.Collections.Generic;
using System.Collections.ObjectModel;
using ComicsViewer.Common;

#nullable enable

namespace MusicPlayer {
    public class PlaylistViewModel : ViewModelBase {
        public ObservableCollection<PlaylistItem> Items { get; } = new();

        internal void SetItems(IEnumerable<PlaylistItem> items) {
            this.Items.Clear();

            foreach (var item in items) {
                this.Items.Add(item);
            }
        }
    }
}
