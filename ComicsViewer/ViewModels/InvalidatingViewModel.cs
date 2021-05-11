using ComicsViewer.Common;

#nullable enable

namespace ComicsViewer.ViewModels {
    public abstract class InvalidatingViewModel : ViewModelBase {
        private bool invalidated = false;

        public virtual void RemoveEventHandlers() {
            this.ThrowIfInvalidated();

            this.invalidated = true;
        }

        public void ThrowIfInvalidated() {
            if (this.invalidated) {
                throw new ProgrammerError("Attempting to use an invalidated view model");
            }
        }
    }
}
