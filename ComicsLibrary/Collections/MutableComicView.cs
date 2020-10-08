using System.Collections.Generic;
using ComicsViewer.Common;

namespace ComicsLibrary.Collections {
    /// <summary>
    /// A convenience class representing mutable collection of comics whose mutation methods are only exposed internally
    /// </summary>
    public abstract class MutableComicView : ComicView {
        // Both AddComic and RemoveComic should panic if comics already exist/don't exist
        private protected abstract void AddComic(Comic comics);
        private protected abstract void RemoveComic(Comic comics);
        private protected abstract void RefreshComics(IEnumerable<Comic> comics);

        private protected MutableComicView(ComicView? trackChangesFrom) : base(trackChangesFrom) { }

        private protected void AddComics(IEnumerable<Comic> comics) {
            foreach (var comic in comics) {
                this.AddComic(comic);
            }
        }

        private protected void RemoveComics(IEnumerable<Comic> comics) {
            foreach (var comic in comics) {
                this.RemoveComic(comic);
            }
        }

        private protected override void ParentComicView_ViewChanged(ComicView sender, ViewChangedEventArgs e) {
            switch (e.Type) {  // switch ChangeType
                case ComicChangeType.ItemsChanged:
                    this.RemoveComics(e.Remove);
                    this.AddComics(e.Add);
                    break;
                case ComicChangeType.Refresh:
                    this.RefreshComics(sender);
                    break;
                case ComicChangeType.ThumbnailChanged:
                    // do nothing; propagate
                    break;
                default:
                    throw new ProgrammerError($"{nameof(MutableComicView)}.{nameof(this.ParentComicView_ViewChanged)}: unhandled switch case");
            }

            this.OnComicChanged(e);
        }
    }
}
