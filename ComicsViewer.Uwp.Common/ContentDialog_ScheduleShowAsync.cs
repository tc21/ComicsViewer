using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ComicsViewer.Common;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace ComicsViewer.Uwp.Common {
    public static class ContentDialog_ScheduleShowAsync {
        private static readonly ConcurrentQueue<ContentDialog> scheduledContentDialogs = new ConcurrentQueue<ContentDialog>();
        private static ContentDialogResult? lastResult = null;

        public static async Task<ContentDialogResult> ScheduleShowAsync(this ContentDialog dialog) {
            scheduledContentDialogs.Enqueue(dialog);

            while (!scheduledContentDialogs.TryPeek(out var first) || first != dialog) {
                await Task.Delay(100);
            }

            lastResult = null;

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                async () => lastResult = await dialog.ShowAsync()
            );

            while (lastResult == null) {
                await Task.Delay(100);
            }

            while (true) {
                if (scheduledContentDialogs.TryDequeue(out var first)) {
                    if (first != dialog) {
                        throw ProgrammerError.Auto();
                    }

                    break;
                }

                await Task.Delay(100);
            }

            return (ContentDialogResult)lastResult;
        }
    }
}
