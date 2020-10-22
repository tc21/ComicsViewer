using System;
using System.Threading.Tasks;
using ComicsViewer.Support;

#nullable enable

namespace ComicsViewer.Pages {
    public class TextInputDialogNavigationArguments {
        public TextInputDialogProperties Properties { get; }
        public string InitialValue { get; }
        public Func<string, Task> AsyncAction { get; }
        public Func<string, ValidateResult>? Validate { get; }

        public TextInputDialogNavigationArguments(
            TextInputDialogProperties properties, 
            string initialValue, 
            Func<string, Task> action, 
            Func<string, ValidateResult>? validate = null
        ) {
            this.Properties = properties;
            this.InitialValue = initialValue;
            this.AsyncAction = action;
            this.Validate = validate;
        }
    }

    public class TextInputDialogProperties {
        public string TextBoxHeader { get; }
        public string SubmitText { get; }
        public string CancelText { get; }

        private TextInputDialogProperties(string textBoxHeader, string submitText, string cancelText) {
            this.TextBoxHeader = textBoxHeader;
            this.SubmitText = submitText;
            this.CancelText = cancelText;
        }

        public static TextInputDialogProperties ForSavingChanges(string propertyName) {
            return new TextInputDialogProperties(propertyName, "Save changes", "Discard changes");
        }
    }
}
