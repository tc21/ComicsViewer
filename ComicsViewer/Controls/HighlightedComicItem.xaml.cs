using System;
using System.Threading.Tasks;
using ComicsViewer.Support;
using ComicsViewer.ViewModels;
using Windows.ApplicationModel.Core;
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

            this.ThumbnailImage.ImageOpened += this.ThumbnailImage_ImageOpened;
        }

        private bool imageOpened = false;

        private void ThumbnailImage_ImageOpened(object sender, RoutedEventArgs e) {
            this.imageOpened = true;
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


        public string? AdditionalInfo {
            get => (string?)this.GetValue(AdditionalInfoProperty);
            set => this.SetValue(AdditionalInfoProperty, value);
        }

        public static readonly DependencyProperty AdditionalInfoProperty =
            DependencyProperty.Register(nameof(AdditionalInfo), typeof(string), typeof(HighlightedComicItem), new PropertyMetadata(null));


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

        public object Footer {
            get => this.GetValue(FooterProperty);
            set => this.SetValue(FooterProperty, value);
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register(nameof(Footer), typeof(object), typeof(HighlightedComicItem), new PropertyMetadata(null));

        public ConnectedAnimation PrepareConnectedAnimationFromThumbnail(ComicItem item) {
            return ConnectedAnimationHelper.PrepareAnimation(this.ThumbnailImage, item, "navigateIn");
        }

        public void TryStartConnectedAnimationToThumbnail(ComicItem item) {
            // When this method is called, the UI of this control may not have been properly initialized yet. 
            // You can see this by hard-coding this.ThumbnailImage.Width, and the setting a breakpoint here.
            // You will see 0: it hasn't finished initializing yet.
            // As a workaround, we delay the starting of the connected animation until the thumbnail image is loaded,
            // so we can guarantee the control is finished loading.
            // Note: the reason for this is, before the image is opened, the ThumbnailImage control has a width and
            // height of 0. Thus any animation would disappear into a dot.
            if (this.imageOpened) {
                _ = ConnectedAnimationHelper.TryStartAnimation(this.ThumbnailImage, item, "navigateIn");
            } else {
                this.ThumbnailImage.ImageOpened += TryStartAnimation;
                
                // Sometimes ImageOpened is invoked before we can bind TryStartAnimation.
                // We wait one second, and then cancel the animation.
                _ = Task.Delay(1000).ContinueWith(async _ => 
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            Windows.UI.Core.CoreDispatcherPriority.Normal, 
                            () => {
                                if (ConnectedAnimationHelper.AnimationExists("navigateIn")) {
                                    ConnectedAnimationHelper.CancelAnimation("navigateIn");
                                }
                            }
                       )
                );
            }

            void TryStartAnimation(object sender, RoutedEventArgs e) {
                if (ConnectedAnimationHelper.AnimationExists("navigateIn")) {
                    _ = ConnectedAnimationHelper.TryStartAnimation(this.ThumbnailImage, item, "navigateIn");
                }

                this.ThumbnailImage.ImageOpened -= TryStartAnimation;
            }
        }
    }
}
