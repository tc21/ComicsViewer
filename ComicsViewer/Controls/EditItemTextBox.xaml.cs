using ComicsViewer.Support;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

#nullable enable

namespace ComicsViewer.Controls {
    public sealed partial class EditItemTextBox : INotifyPropertyChanged {
        public EditItemTextBox() {
            this.InitializeComponent();
        }

        private void Reset() {
            this.IsEditing = false;
            this.IsContentModified = false;
            this.IsContentValid = true;
            this.ErrorText = null;
            this.TextBox.Text = this.GetText();
            this.UneditedTextBox.Text = this.GetText();
        }

        private async Task Save() {
            this.IsEnabled = false;

            if (this.SaveItemValue != null) {
                this.SaveItemValue(this.TextBox.Text);
            } else if (this.SaveItemValueAsync != null) {
                await this.SaveItemValueAsync(this.TextBox.Text);
            }

            this.IsEnabled = true;
        }

        public void RegisterHandlers(Func<string> get, Func<string, Task> saveAsync, Func<string, ValidateResult>? validate = null) {
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

        private bool saveButtonSet;
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


        public Func<string, ValidateResult>? ValidateWithReason {
            get => this.GetValue(ValidateWithReasonProperty) as Func<string, ValidateResult>;
            set => this.SetValue(ValidateWithReasonProperty, value);
        }

        public static readonly DependencyProperty ValidateWithReasonProperty =
            DependencyProperty.Register(nameof(ValidateWithReason), typeof(Func<string, ValidateResult>), typeof(EditItemTextBox), new PropertyMetadata(null));

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
                this.OnPropertyChanged("");
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
                this.OnPropertyChanged("");
            }
        }

        public static readonly DependencyProperty SaveItemValueAsyncProperty =
            DependencyProperty.Register(nameof(SaveItemValueAsync), typeof(Func<string, Task>), typeof(EditItemTextBox), new PropertyMetadata(null));


        #endregion

        #region Other properties

        private bool _isEditing;
        private bool IsEditing {
            get => this._isEditing;
            set {
                this._isEditing = value;

                this.OnPropertyChanged("");
            }
        }

        private bool _isContentModified;
        private bool IsContentModified {
            get => this._isContentModified;
            set {
                this._isContentModified = value;
                this.UpdateSaveButtonState();
                this.OnPropertyChanged(nameof(this.TextBoxQueryIcon));
            }
        }

        private bool _isContentValid;
        private bool IsContentValid {
            get => this._isContentValid;
            set {
                this._isContentValid = value;
                this.UpdateSaveButtonState();
            }
        }

        private string? _errorText;
        public string? ErrorText {
            get => this._errorText ?? this.WarningText;
            private set { 
                this._errorText = value;
                this.OnPropertyChanged();
                this.OnPropertyChanged(nameof(this.IsErrorIndicatorVisible));

                this.TextBox.QueryIcon = new SymbolIcon(Symbol.Refresh);
            }
        }

        private readonly IconElement refreshIcon = new SymbolIcon(Symbol.Refresh);

        private bool IsEditable => this.SaveItemValue != null || this.SaveItemValueAsync != null;
        private bool IsEditTextBoxVisible => this.IsEditable && (!this.RequiresInteraction || this.IsEditing);
        private bool IsUneditedTextBlockVisible => !this.IsEditTextBoxVisible;
        private bool IsHeaderVisible => this.Header != null;
        private bool IsErrorIndicatorVisible => this.IsContentModified && this.ErrorText != null;
        private IconElement? TextBoxQueryIcon => this.IsContentModified || this.RequiresInteraction ? this.refreshIcon : null;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "") {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int MinTextBoxWidth {
            get => (int)this.GetValue(MinTextBoxWidthProperty);
            set => this.SetValue(MinTextBoxWidthProperty, value);
        }

        public static readonly DependencyProperty MinTextBoxWidthProperty =
            DependencyProperty.Register(nameof(MinTextBoxWidth), typeof(int), typeof(EditItemTextBox), new PropertyMetadata(0));

        #endregion

        #region Event handlers

        private void TextBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) {
                return;
            }

            this.IsContentModified = true;

            if (this.ValidateWithReason?.Invoke(sender.Text) is { } result) {
                this.IsContentValid = result;
                this.ErrorText = result.Comment;
            } else {
                this.IsContentValid = true;
                this.ErrorText = null;
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
            
            this.Reset();
            this.UpdateSaveButtonState();
        }

        #endregion
    }
}
