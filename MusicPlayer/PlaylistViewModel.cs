#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using ComicsViewer.Common;

namespace MusicPlayer {
    public class PlaylistViewModel : ViewModelBase {
        public ObservableCollection<PlaylistItem> Items { get; } = new ObservableCollection<PlaylistItem>();

        internal void SetItems(IEnumerable<PlaylistItem> items) {
            this.Items.Clear();

            foreach (var item in items) {
                this.Items.Add(item);
            }
        }
    }
}
