using System;
using ComicsViewer.Pages;
using ComicsViewer.Support;
using ComicsViewer.ViewModels.Pages;
using Windows.UI.Xaml.Controls;

#nullable enable

namespace ComicsViewer {
    public interface IMainPageContent {
        NavigationTag NavigationTag { get; }
        NavigationPageType NavigationPageType { get; }
        Page Page { get; }
        ComicItemGrid? ComicItemGrid { get; }
        string? PageName { get; }

        event Action<IMainPageContent>? Initialized;
        Action NavigateOut { get; }
    }
}