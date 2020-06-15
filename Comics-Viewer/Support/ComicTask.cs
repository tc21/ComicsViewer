using ComicsViewer.Support.ClassExtensions;
using ComicsViewer.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;

#nullable enable

namespace ComicsViewer.ViewModels {
    public class ComicTask : ViewModelBase {
        public string Name { get; }
        public string Status { get; private set; } = "initialzed";
        public bool IsCancelled { get; private set; } = false;
        public bool IsCompleted { get; private set; } = false;
        public bool IsFaulted { get; private set; } = false;
        private Exception? storedException;

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
            this.task = Task.Run(() => userAction(this.cancellationTokenSource.Token, this.progress)).ContinueWith(async finishedTask => {
                if (finishedTask.IsCompletedSuccessfully) {
                    this.IsCompleted = true;
                    this.Status = "completed";
                } else if (finishedTask.IsFaulted) {
                    this.IsFaulted = true;
                    this.storedException = finishedTask.Exception.InnerException;
                    this.Status = "faulted";
                } else {
                    throw new ApplicationLogicException();
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    this.OnPropertyChanged(nameof(this.Status));
                    this.TaskCompleted(this, this.IsCompleted ? finishedTask.Result : null);
                });

            }, this.cancellationTokenSource.Token);


            this.Status = "started";
            this.OnPropertyChanged(nameof(this.Status));
        }

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
                if (this.task.Status == TaskStatus.WaitingForActivation || task.Status == TaskStatus.Running) {
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

        public void ThrowStoredException() {
            if (this.storedException == null) {
                throw new ArgumentException();
            }

            throw this.storedException;
        }

        public event Action<ComicTask, object?> TaskCompleted = delegate { };

        public void CancelTaskButton_Click(object sender, RoutedEventArgs e) {
            this.Cancel();
        }
    }
}
