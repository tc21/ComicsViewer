using ComicsViewer.XamlHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer.Controls {
    public class ComicsMenuFlyoutItem : MenuFlyoutItem {
        public ComicsMenuFlyoutItem() {
            Bindings.SetVisibilityToEnabled(this, true);
        }

        public string? FontIcon {
            get => this.GetValue(FontIconProperty) as string;
            set {
                this.SetValue(FontIconProperty, value);
                this.Icon = new FontIcon() { Glyph = value };
            }
        }

        public static readonly DependencyProperty FontIconProperty =
            DependencyProperty.Register(nameof(FontIcon), typeof(string), typeof(ComicsMenuFlyoutItem), new PropertyMetadata(null));

        public Symbol SymbolIcon {
            get =>(Symbol)this.GetValue(SymbolIconProperty);
            set {
                this.SetValue(SymbolIconProperty, value);
                this.Icon = new SymbolIcon { Symbol = value };
            }
        }

        public static readonly DependencyProperty SymbolIconProperty =
            DependencyProperty.Register(nameof(SymbolIcon), typeof(Symbol), typeof(ComicsMenuFlyoutItem), new PropertyMetadata(null));
    }
}
