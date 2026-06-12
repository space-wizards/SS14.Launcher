using System;
using System.Linq;
using CodeHollow.FeedReader;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Localization;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public partial class NewsTabViewModel : MainWindowTabViewModel
{
    public ObservableList<NewsEntryViewModel> NewsEntries { get; } = [];
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
            return;

        _startedPullingNews = true;
        var feed = await FeedReader.ReadAsync(ConfigConstants.NewsFeedUrl);

        NewsEntries.AddRange(feed.Items.Select(i => new NewsEntryViewModel(i.Title, new Uri(i.Link))));
        NewsPulled = true;
    }
}
