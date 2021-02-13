using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;


#nullable enable

namespace ComicsViewer.Controls {
    public sealed partial class HighlightedComicItem : UserControl {
        // For our purposes, we will hide the entire usercontrol if Item is set to null. 
        // Also, this control is not designed to be modified at runtime. 
        public HighlightedComicItem() {
            this.InitializeComponent();
        }

        public ComicItem? Item {
            get => (ComicItem?)this.GetValue(ItemProperty);
            set {
                this.SetValue(ItemProperty, value);

                if (value is null) {
                    this.Visibility = Visibility.Collapsed;
                } else {
                    this.Visibility = Visibility.Visible;
                }
            }
        }

        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.Register(nameof(Item), typeof(ComicItem), typeof(HighlightedComicItem), new PropertyMetadata(null));


        public int ImageHeight {
            get => (int)this.GetValue(ImageHeightProperty);
            set => this.SetValue(ImageHeightProperty, value);
        }

        public static readonly DependencyProperty ImageHeightProperty =
            DependencyProperty.Register(nameof(ImageHeight), typeof(int), typeof(HighlightedComicItem), new PropertyMetadata(0));


        public int ImageWidth {
            get => (int)this.GetValue(ImageWidthProperty);
            set => this.SetValue(ImageWidthProperty, value);
        }

        public static readonly DependencyProperty ImageWidthProperty =
            DependencyProperty.Register(nameof(ImageWidth), typeof(int), typeof(HighlightedComicItem), new PropertyMetadata(0));


        public bool TryStartConnectedAnimationToThumbnail(ComicItem item) {
            return ConnectedAnimationHelper.TryStartAnimation(this.ThumbnailImage, item, "navigateIn");
        }
    }
}
