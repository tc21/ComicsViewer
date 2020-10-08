using System;

namespace ComicsViewer.Support {
    public abstract class DeferredNotify {
        // Based on Microsoft.Toolkit.Uwp.UI.AdvancedCollectionView
        private bool deferNotifications;

        protected abstract void DoNotify();

        protected void SendNotification() {
            if (!this.deferNotifications) {
                this.DoNotify();
            }
        }

        public IDisposable DeferNotifications() {
            return new NotificationDeferrer(this);
        }

        private class NotificationDeferrer : IDisposable {
            private readonly DeferredNotify obj;

            public NotificationDeferrer(DeferredNotify obj) {
                this.obj = obj;
                this.obj.deferNotifications = true;
            }

            public void Dispose() {
                this.obj.deferNotifications = false;
                this.obj.SendNotification();
            }
        }
    }
}
