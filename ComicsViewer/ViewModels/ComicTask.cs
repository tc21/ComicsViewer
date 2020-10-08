using ComicsViewer.ClassExtensions;
using ComicsViewer.Common;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicTask : ViewModelBase {
        public string Name { get; }
        public string Status { get; private set; } = "initialized";
        public bool IsCancelled { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsFaulted => this.StoredException != null;
        public Exception? StoredException { get; private set; }

        private readonly ComicTaskDelegate<object> userAction;
        private readonly Progress<int> progress = new Progress<int>();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Task? task;

        public delegate Task ComicTaskDelegate(CancellationToken cancellationToken, IProgress<int> progress);
        public delegate Task<T> ComicTaskDelegate<T>(CancellationToken cancellationToken, IProgress<int> progress);

        public ComicTask(string name, ComicTaskDelegate<object> task) {
            this.Name = name;
            this.userAction = task;
            this.progress.ProgressChanged += this.Progress_ProgressChanged;
        }

        private void Progress_ProgressChanged(object sender, int e) {
            this.Status = e.PluralString("item") + " processed";
            this.OnPropertyChanged(nameof(this.Status));
        }

        public void Start() {
            this.task = Task.Run(() => this.userAction(this.cancellationTokenSource.Token, this.progress)).ContinueWith(async finishedTask => {
                if (finishedTask.IsCompletedSuccessfully) {
                    this.IsCompleted = true;
                    this.Status = "completed";
                } else if (finishedTask.IsFaulted) {
                    this.StoredException = finishedTask.Exception.InnerException;
                    this.Status = "faulted";
                } else {
                    throw new ProgrammerError();
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    this.OnPropertyChanged(nameof(this.Status));
                    this.TaskCompleted(this, this.IsCompleted ? finishedTask.Result : null);
                });

            }, this.cancellationTokenSource.Token);


            this.Status = "started";
            this.OnPropertyChanged(nameof(this.Status));
        }

        /// returns true if the task was canceled. returns false if the task was already faulted or completed.
        public bool Cancel() {
            if (this.task == null) {
                throw new ArgumentException("Cannot cancel a task that hasn't been started");
            }

            if (this.IsCancelled) {
                throw new ArgumentException("Cannot cancel a task that has already been cancelled");
            }

            if (this.IsCompleted || this.IsFaulted) {
                return false;
            }

            this.IsCancelled = true;

            this.Status = "cancelling";
            this.OnPropertyChanged(nameof(this.Status));

            this.cancellationTokenSource.Cancel();

            _ = Task.Run(async () => {
                if (this.task.Status == TaskStatus.WaitingForActivation || this.task.Status == TaskStatus.Running) {
                    await this.task;
                }

                this.Status = "cancelled";

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    this.OnPropertyChanged(nameof(this.Status));
                    this.TaskCompleted(this, null);
                });

            });


            return true;
        }

        public event Action<ComicTask, object?> TaskCompleted = delegate { };

        public void CancelTaskButton_Click(object sender, RoutedEventArgs e) {
            _ = this.Cancel();
        }
    }
}
