﻿using ComicsViewer.ViewModels.Pages;
using System;
using Windows.UI.Xaml.Navigation;

#nullable enable

namespace ComicsViewer.Pages {
    public class ComicItemGridNavigationArguments {
        public ComicItemGridViewModel? ViewModel { get; set; }
        public Action<ComicItemGrid, NavigationEventArgs>? OnNavigatedTo { get; set; }
    }
}
