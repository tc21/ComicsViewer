﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Controls {
    public sealed partial class EditItemTextBox : UserControl, INotifyPropertyChanged {
        public EditItemTextBox() {
            this.InitializeComponent();
        }

        public void Reset() {
            this.IsEditing = false;
            this.IsContentModified = false;
            this.OnPropertyChanged(nameof(this.Text));
        }

        public async Task Save() {
            this.IsEnabled = false;

            if (this.SaveItemValue != null) {
                this.SaveItemValue(this.TextBox.Text);
            } else if (this.SaveItemValueAsync != null) {
                await this.SaveItemValueAsync(this.TextBox.Text);
            } else {
                throw new InvalidOperationException($"{nameof(EditItemTextBox)} called {nameof(Save)} without a handler");
            }

            this.IsEnabled = true;
        }

        public void RegisterHandlers(Func<string> get, Action<string> save, Func<string, string?>? validate = null) {
            this.GetItemValue = get;
            this.SaveItemValue = save;
            this.ValidateWithReason = validate;
        }

        public void RegisterHandlers(Func<string> get, Func<string, Task> saveAsync, Func<string, string?>? validate = null) {
            this.GetItemValue = get;
            this.SaveItemValueAsync = saveAsync;
            this.ValidateWithReason = validate;
        }

        private string GetText() {
            return this.GetItemValue?.Invoke() ?? "<error: unset>";
        }

        private void UpdateSaveButtonState() {
            if (this.SaveButton == null) {
                this.InlineSaveButton.Visibility = this.IsEditTextBoxVisible ? Visibility.Visible : Visibility.Collapsed;
                this.InlineSaveButton.IsEnabled = this.IsContentModified && this.IsContentValid;
            } else {
                this.InlineSaveButton.Visibility = Visibility.Collapsed;
                this.SaveButton.IsEnabled = this.IsContentModified && this.IsContentValid;
            }
        }

        #region Dependency properties

        public string? Header {
            get => this.GetValue(HeaderProperty) as string;
            set {
                this.SetValue(HeaderProperty, value);
                this.OnPropertyChanged(nameof(this.IsHeaderVisible));
            }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(EditItemTextBox), new PropertyMetadata(null));

        private bool saveButtonSet = false;
        public ButtonBase? SaveButton {
            get => this.GetValue(SaveButtonProperty) as ButtonBase;
            set {
                // For now, we will not support changing save buttons
                if (this.saveButtonSet) {
                    throw new InvalidOperationException($"{nameof(EditItemTextBox)} does not support setting {nameof(this.SaveButton)} more than once");
                }

                if (value != null) {
                    value.Tapped += this.SaveButton_Tapped;
                }

                this.saveButtonSet = true;
                this.UpdateSaveButtonState();
                this.SetValue(SaveButtonProperty, value);
            }
        }

        public static readonly DependencyProperty SaveButtonProperty =
            DependencyProperty.Register(nameof(SaveButton), typeof(ButtonBase), typeof(EditItemTextBox), new PropertyMetadata(null));

        public bool RequiresInteraction {
            get => (bool)this.GetValue(RequiresInteractionProperty);
            set {
                this.SetValue(RequiresInteractionProperty, value);
                this.IsEditing = this._isEditing;
            }
        }

        public static readonly DependencyProperty RequiresInteractionProperty =
            DependencyProperty.Register(nameof(RequiresInteraction), typeof(bool), typeof(EditItemTextBox), new PropertyMetadata(false));

        public string? WarningText {
            get => this.GetValue(WarningTextProperty) as string;
            set {
                this.SetValue(WarningTextProperty, value);
                this.ErrorText = this._errorText;
            }
        }

        public static readonly DependencyProperty WarningTextProperty =
            DependencyProperty.Register(nameof(WarningText), typeof(string), typeof(EditItemTextBox), new PropertyMetadata(null));


        public Func<string, string?>? ValidateWithReason {
            get => this.GetValue(ValidateWithReasonProperty) as Func<string, string?>;
            set => this.SetValue(ValidateWithReasonProperty, value);
        }

        public static readonly DependencyProperty ValidateWithReasonProperty =
            DependencyProperty.Register(nameof(ValidateWithReason), typeof(Func<string, string?>), typeof(EditItemTextBox), new PropertyMetadata(null));

        public Func<string>? GetItemValue {
            get => this.GetValue(GetItemValueProperty) as Func<string>;
            set => this.SetValue(GetItemValueProperty, value);
        }

        public static readonly DependencyProperty GetItemValueProperty =
            DependencyProperty.Register(nameof(GetItemValue), typeof(Func<string>), typeof(EditItemTextBox), new PropertyMetadata(null));

        public Action<string>? SaveItemValue {
            get => this.GetValue(SaveItemValueProperty) as Action<string>;
            set {
                if (this.SaveItemValueAsync != null) {
                    throw new ArgumentException($"cannot set {nameof(this.SaveItemValue)} when {nameof(this.SaveItemValueAsync)} is not null");
                }
                this.SetValue(SaveItemValueProperty, value);
            }
        }

        public static readonly DependencyProperty SaveItemValueProperty =
            DependencyProperty.Register(nameof(SaveItemValue), typeof(Action<string>), typeof(EditItemTextBox), new PropertyMetadata(null));

        public Func<string, Task>? SaveItemValueAsync {
            get => this.GetValue(SaveItemValueAsyncProperty) as Func<string, Task>;
            set {
                if (this.SaveItemValueAsync != null) {
                    throw new ArgumentException($"cannot set {nameof(this.SaveItemValueAsync)} when {nameof(this.SaveItemValue)} is not null");
                }

                this.SetValue(SaveItemValueAsyncProperty, value);
            }
        }

        public static readonly DependencyProperty SaveItemValueAsyncProperty =
            DependencyProperty.Register(nameof(SaveItemValueAsync), typeof(Func<string, Task>), typeof(EditItemTextBox), new PropertyMetadata(null));


        #endregion

        #region Other properties

        private bool _isEditing = false;
        private bool IsEditing {
            get => this._isEditing;
            set {
                this._isEditing = value;

                this.OnPropertyChanged();
                this.OnPropertyChanged(nameof(this.IsEditTextBoxVisible));
                this.OnPropertyChanged(nameof(this.IsUneditedTextBlockVisible));
            }
        }

        private bool _isContentModified;
        public bool IsContentModified {
            get => this._isContentModified;
            set {
                this._isContentModified = value;
                this.UpdateSaveButtonState();
                this.OnPropertyChanged(nameof(this.TextBoxQueryIcon));
            }
        }

        private bool _isContentValid;
        public bool IsContentValid {
            get => this._isContentValid;
            set {
                this._isContentValid = value;
                this.UpdateSaveButtonState();
            }
        }

        private string? _errorText;
        public string? ErrorText {
            get => this._errorText ?? this.WarningText;
            set { 
                this._errorText = value;
                this.OnPropertyChanged();
                this.OnPropertyChanged(nameof(this.IsErrorIndicatorVisible));

                this.TextBox.QueryIcon = new SymbolIcon(Symbol.Refresh);
            }
        }

        private static readonly IconElement RefreshIcon = new SymbolIcon(Symbol.Refresh);

        public string Text => this.GetText();

        private bool IsEditTextBoxVisible => !this.RequiresInteraction || this.IsEditing;
        private bool IsUneditedTextBlockVisible => !this.IsEditTextBoxVisible;
        private bool IsHeaderVisible => this.Header != null;
        private bool IsErrorIndicatorVisible => this.IsContentModified && this.ErrorText != null;
        private IconElement? TextBoxQueryIcon => this.IsContentModified ? RefreshIcon : null;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "") {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Event handlers

        private void TextBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) {
                if (this.ValidateWithReason?.Invoke(sender.Text) is string reason) {
                    this.IsContentValid = false;
                    this.ErrorText = reason;
                } else {
                    this.IsContentValid = true;
                }

                this.IsContentModified = true;
            }
        }

        private void StartEditButton_Tapped(object sender, TappedRoutedEventArgs e) {
            this.IsEditing = true;
        }

        private void TextBox_Reset(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) {
            this.Reset();
        }

        private async void SaveButton_Tapped(object sender, TappedRoutedEventArgs e) {
            await this.Save();
            this.Reset();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            // Make sure its set, since there's no runtime check. If you make this an exception then XAML previews will fail.
            Debug.WriteLine($"Warning: {nameof(EditItemTextBox)} was loaded without setting the property {nameof(this.GetItemValue)}");
            
            this.UpdateSaveButtonState();
        }

        #endregion
    }
}
