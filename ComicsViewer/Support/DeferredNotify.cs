using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicsViewer.Support {
    public abstract class DeferredNotify {
        // Based on Microsoft.Toolkit.Uwp.UI.AdvancedCollectionView
        private bool deferNotifications = false;

        protected abstract void DoNotify();

        protected void SendNotification() {
            if (!this.deferNotifications) {
                this.DoNotify();
            }
        }

        public IDisposable DeferNotifications() {
            return new NotificationDeferrer(this);
        }

        public class NotificationDeferrer : IDisposable {
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
