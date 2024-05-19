using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodeHollow.FeedReader;
using ReactiveUI;
using SS14.Launcher.Localization;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class NewsTabViewModel : MainWindowTabViewModel
{
    private const string FeedUrl = "https://spacestation14.io/post/index.xml";

    private bool _startedPullingNews;
    private bool _newsPulled;

    public NewsTabViewModel()
    {
        NewsEntries = new ObservableCollection<NewsEntryViewModel>(new List<NewsEntryViewModel>());

        this.WhenAnyValue(x => x.NewsPulled)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(NewsNotPulled)));
    }

    public bool NewsPulled
    {
        get => _newsPulled;
        set => this.RaiseAndSetIfChanged(ref _newsPulled, value);
    }

    public bool NewsNotPulled => !NewsPulled;

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
        var feed = await FeedReader.ReadAsync(FeedUrl);

        foreach (var feedItem in feed.Items)
        {
            NewsEntries.Add(new NewsEntryViewModel(feedItem.Title, new Uri(feedItem.Link)));
        }

        NewsPulled = true;
    }

    public ObservableCollection<NewsEntryViewModel> NewsEntries { get; }

    public override string Name => LocalizationManager.Instance.GetString("tab-news-title");
}
