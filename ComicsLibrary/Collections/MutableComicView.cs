using System;
using System.Collections.Generic;
using System.Text;

namespace ComicsLibrary.Collections {
    /// <summary>
    /// A convenience class representing mutable collection of comics whose mutation methods are only exposed internally
    /// </summary>
    public abstract class MutableComicView : ComicView {
        // Both AddComic and RemoveComic should panic if comics already exist/don't exist
        private protected abstract void AddComic(Comic comics);
        private protected abstract void RemoveComic(Comic comics);
        private protected abstract void RefreshComics(IEnumerable<Comic> comics);

        private readonly ComicView? parent;

        private protected MutableComicView(ComicView? trackChangesFrom) : base(trackChangesFrom) {
            this.parent = trackChangesFrom;
        }

        private protected virtual void AddComics(IEnumerable<Comic> comics) {
            foreach (var comic in comics) {
                this.AddComic(comic);
            }
        }

        private protected virtual void RemoveComics(IEnumerable<Comic> comics) {
            foreach (var comic in comics) {
                this.RemoveComic(comic);
            }
        }

        private protected override void ParentComicView_ViewChanged(ComicView sender, ViewChangedEventArgs e) {
            switch (e.Type) {  // switch ChangeType
                case ChangeType.ItemsChanged:
                    this.RemoveComics(e.Remove);
                    this.AddComics(e.Add);
                    break;
                case ChangeType.Refresh:
                    this.RefreshComics(sender);
                    break;
                case ChangeType.ThumbnailChanged:
                    // do nothing; propagate
                    break;
                default:
                    throw new ProgrammerError($"{nameof(MutableComicView)}.{nameof(ParentComicView_ViewChanged)}: unhandled switch case");
            }

            this.OnComicChanged( e);
        }
    }
}
