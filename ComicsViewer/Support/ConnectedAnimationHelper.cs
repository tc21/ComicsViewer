using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComicsViewer.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace ComicsViewer.Support {
    public static class ConnectedAnimationHelper {
        private static readonly Dictionary<string, string> preparedAnimationComicItems = new();

        public static ConnectedAnimation PrepareAnimation(UIElement source, ComicItem comicItem, string key) {
            if (AnimationExists(key)) {
                throw new ArgumentException("An animation with this key was previously prepared, but never started");
            }

            var comicId = comicItem.ContainedComics().First().UniqueIdentifier;
            preparedAnimationComicItems.Add(key, comicId);

            return ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(key, source);
        }

        public static ConnectedAnimation PrepareAnimationFromListView(ListViewBase source, string elementName, ComicItem comicItem, string key) {
            if (AnimationExists(key)) {
                throw new ArgumentException("An animation with this key was previously prepared, but never started");
            }

            var comicId = comicItem.ContainedComics().First().UniqueIdentifier;
            preparedAnimationComicItems.Add(key, comicId);

            return source.PrepareConnectedAnimation(key, comicItem, elementName);
        }

        public static bool TryStartAnimation(UIElement destination, ComicItem comicItem, string key) {
            if (!AnimationExists(key)) {
                throw new ArgumentException("An animation with this key was never prepared");
            }

            if (ConnectedAnimationService.GetForCurrentView().GetAnimation(key) is not { } animation) {
                return false;
            }

            var comicId = comicItem.ContainedComics().First().UniqueIdentifier;
            var success = comicId == preparedAnimationComicItems[key] && animation.TryStart(destination);

            if (!success) {
                animation.Cancel();
            }

            _ = preparedAnimationComicItems.Remove(key);

            return success;
        }

        public static async Task<bool> TryStartAnimationToListViewAsync(ListViewBase destination, string elementName, ComicItem comicItem, string key
        ) {
            if (!AnimationExists(key)) {
                throw new ArgumentException("An animation with this key was never prepared");
            }

            if (ConnectedAnimationService.GetForCurrentView().GetAnimation(key) is not { } animation) {
                return false;
            }

            var comicId = comicItem.ContainedComics().First().UniqueIdentifier;
            var success = comicId == preparedAnimationComicItems[key] &&
                await destination.TryStartConnectedAnimationAsync(animation, comicItem, elementName);

            if (!success) {
                animation.Cancel();
            }

            _ = preparedAnimationComicItems.Remove(key);

            return success;
        }

        public static bool AnimationExists(string key) {
            return preparedAnimationComicItems.ContainsKey(key);
        }

        public static void CancelAnimation(string key) {
            if (!AnimationExists(key)) {
                throw new ArgumentException("An animation with this key was never prepared");
            }

            if (ConnectedAnimationService.GetForCurrentView().GetAnimation(key) is { } animation) {
                // If the animation was prepared, but not started in a short amount of time, it could have expired.
                animation.Cancel();
            }

            _ = preparedAnimationComicItems.Remove(key);
        }
    }
}
