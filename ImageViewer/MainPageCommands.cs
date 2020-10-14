using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Windows.Graphics.Display;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ImageViewer {
    public partial class MainPage {
        private ICommand? seekRelativeCommand;
        public ICommand SeekRelativeCommand {
            get {
                this.seekRelativeCommand ??= new RelayCommand(
                    async o => await this.ViewModel.SeekAsync(this.ViewModel.CurrentImageIndex + (int)TryParseInt(o)!),
                    o => {
                        if (TryParseInt(o) is { } i) {
                            return i > -this.ViewModel.Images.Count && i < this.ViewModel.Images.Count;
                        }

                        return false;
                    }
                );

                return this.seekRelativeCommand;
            }
        }

        private ICommand? seekCommand;
        public ICommand SeekCommand {
            get {
                this.seekCommand ??= new RelayCommand(
                    async o => await this.ViewModel.SeekAsync((int)TryParseInt(o)!),
                    o => {
                        if (TryParseInt(o) is { } target) {
                            // see ActualIndex(): it is designed to only work if target > -Images.Count
                            // we could have made it work with any number, but you're probably doing something wrong if it
                            // goes out of this range
                            return target > -this.ViewModel.Images.Count && target < this.ViewModel.Images.Count;
                        }

                        return false;
                    }
                );
                

                return this.seekCommand;
            }
        }

        private ICommand? showInExplorerCommand;
        public ICommand ShowInExplorerCommand {
            get {
                this.showInExplorerCommand ??= new RelayCommand(
                    async o => await Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(this.ViewModel.CurrentImagePath)),
                    this.IsViewingOpenFile
                );

                return this.showInExplorerCommand;
            }
        }

        private ICommand? _toggleImageInfoCommand;
        public ICommand ToggleImageInfoCommand {
            get {
                this._toggleImageInfoCommand ??= new RelayCommand(
                    o => this.ViewModel.IsMetadataVisible = !this.ViewModel.IsMetadataVisible
                );

                return this._toggleImageInfoCommand;
            }
        }

        private ICommand? deleteCommand;
        public ICommand DeleteCommand {
            get {
                this.deleteCommand ??= new RelayCommand(
                    async _ => {
                        // this doesn't work currently because the image is still loaded and the file handle is still held!
                        var response = await new ContentDialog {
                            Title = "Confirm file deletion",
                            Content = "Are you sure you want to move this file to the Recycle Bin?",
                            CloseButtonText = "No",
                            PrimaryButtonText = "Yes",
                            DefaultButton = ContentDialogButton.Close
                        }.ShowAsync();

                        if (response != ContentDialogResult.Primary) {
                            return;
                        }

                        await this.ViewModel.DeleteCurrentImageAsync();
                    },
                    this.IsViewingOpenFile
                );

                return this.deleteCommand;
            }
        }

        private ICommand? closeWindowCommand;
        public ICommand CloseWindowCommand {
            get {
                this.closeWindowCommand ??= new RelayCommand(
                    async _ => await ApplicationView.GetForCurrentView().TryConsolidateAsync()
                );

                return this.closeWindowCommand;
            }
        }

        private ICommand? zoomCommand;
        public ICommand ZoomCommand {
            get {
                this.zoomCommand ??= new RelayCommand(
                    val => this.ZoomImage(double.Parse((string)val))
                );
                
                return this.zoomCommand;
            }
        }

        private ICommand? resetZoomCommand;
        public ICommand ResetZoomCommand {
            get {
                this.resetZoomCommand ??= new RelayCommand(
                    val => this.ResetZoom()
                );
                
                return this.resetZoomCommand;
            }
        }

        private ICommand? _toggleScalingCommand;
        public ICommand ToggleScalingCommand {
            get {
                this._toggleScalingCommand ??= new RelayCommand(
                    val => {
                        if (this.ToggleScalingFlyoutItem.IsChecked) {
                            this.UpdateDecodeImageHeight();
                        } else {
                            this.ViewModel.DecodeImageHeight = null;
                        }

                        Settings.Set(Settings.ScalingEnabledProperty, this.ToggleScalingFlyoutItem.IsChecked);
                    }
                );
                return this._toggleScalingCommand;
            }
        }

        private ICommand? _seekToImageCommand;
        public ICommand SeekToImageCommand {
            get {
                this._seekToImageCommand ??= new RelayCommand(
                    async val => {
                        // 1-indexed
                        this.SeekToImageTextBox.Text = (this.ViewModel.CurrentImageIndex + 1).ToString();
                        this.SeekToImageTextBox.SelectAll();
                        if (await this.SeekToImageDialog.ShowAsync() == ContentDialogResult.Primary
                                && int.TryParse(this.SeekToImageTextBox.Text, out var i)) {
                            await this.ViewModel.SeekAsync(i - 1);
                        }
                    },
                    val => this.ViewModel.CanSeek
                );

                return this._seekToImageCommand;
            }
        }

        private bool IsViewingOpenFile(object _) => this.ViewModel.CurrentImageIndex < this.ViewModel.Images.Count;

        private static int? TryParseInt(object o) {
            int? i = null;

            switch (o) {
                case int i1:
                    i = i1;
                    break;
                case string s: {
                    var success = int.TryParse(s, out var i2);
                    if (success) {
                        i = i2;
                    }

                    break;
                }
            }

            return i;
        }

        private VirtualKey VkPlus => (VirtualKey)0xBB;
        private VirtualKey VkMinus => (VirtualKey)0xBD;
    }

    internal class RelayCommand : ICommand {
        internal static readonly List<RelayCommand> CreatedCommands = new List<RelayCommand>();

        // reference: https://stackoverflow.com/questions/1468791
        private readonly Predicate<object>? _canExecute;
        private readonly Action<object> _execute;


        public RelayCommand(Action<object> execute, Predicate<object>? canExecute = null) {
            this._execute = execute;
            this._canExecute = canExecute;

            CreatedCommands.Add(this);
        }

        public event EventHandler? CanExecuteChanged;
        internal void OnCanExecuteChanged() {
            this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Execute(object parameter) => this._execute(parameter);
        public bool CanExecute(object parameter) => this._canExecute?.Invoke(parameter) ?? true;
    }
}
