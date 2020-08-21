using Microsoft.Toolkit.Uwp.UI.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace ComicsViewer.XamlHelpers {
    public static class Bindings {
        public static readonly DependencyProperty VisibilityToEnabledProperty = 
            DependencyProperty.RegisterAttached("VisibilityToEnabled", typeof(bool), typeof(Bindings), 
                new PropertyMetadata(defaultValue: false, propertyChangedCallback: OnVisibilityToEnabledChanged));
        
        public static bool GetVisibilityToEnabled(DependencyObject obj) {
            return (bool)obj.GetValue(VisibilityToEnabledProperty);
        }

        public static void SetVisibilityToEnabled(DependencyObject obj, bool value) {
            obj.SetValue(VisibilityToEnabledProperty, value);
        }

        private static void OnVisibilityToEnabledChanged(object sender, DependencyPropertyChangedEventArgs args) {
            if (!(sender is Control control)) {
                throw new ProgrammerError("This binding is not valid except on controls");
            }

            if ((bool)args.NewValue) {
                var binding = new Binding {
                    Source = control,
                    Path = new PropertyPath(nameof(Control.IsEnabled)),
                    Converter = new BoolToVisibilityConverter()
                };

                control.SetBinding(UIElement.VisibilityProperty, binding);
            } else {
                control.ClearValue(UIElement.VisibilityProperty);
            }
        }
    }
}
