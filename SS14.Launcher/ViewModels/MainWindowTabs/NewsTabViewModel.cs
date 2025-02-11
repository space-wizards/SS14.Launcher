using System;
using System.Collections.ObjectModel;
using CodeHollow.FeedReader;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Localization;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public partial class NewsTabViewModel : MainWindowTabViewModel
{
    public ObservableCollection<NewsEntryViewModel> NewsEntries { get; } = new ([]);
    public override string Name => LocalizationManager.Instance.GetString("tab-news-title");

    private bool _startedPullingNews;

    [ObservableProperty]
    private bool _newsPulled;

    public override void Selected()
    {
        base.Selected();

        PullNews();
    }

    private async void PullNews()
    {
        if (_startedPullingNews)
        {
            return;
        }

        _startedPullingNews = true;
        var feed = await FeedReader.ReadAsync(ConfigConstants.NewsFeedUrl);

        foreach (var feedItem in feed.Items)
        {
            NewsEntries.Add(new NewsEntryViewModel(feedItem.Title, new Uri(feedItem.Link)));
        }

        NewsPulled = true;
    }
}
