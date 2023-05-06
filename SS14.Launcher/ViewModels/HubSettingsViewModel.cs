using System.Collections.ObjectModel;

namespace SS14.Launcher.ViewModels;

public class HubSettingsViewModel : ViewModelBase
{
    public ObservableCollection<Hub> HubList { get; set; } = new();

    public void Save()
    {
        // TODO
    }

    public void Populate()
    {
        // TODO
    }

    private void Add()
    {
        HubList.Add(new Hub("", this));
    }

    private void Reset()
    {
        HubList.Clear();
        foreach (var url in ConfigConstants.DefaultHubUrls)
        {
            HubList.Add(new Hub(url, this));
        }
    }
}

public class Hub : ViewModelBase
{
    public string Uri { get; set; }
    private readonly HubSettingsViewModel _parentVm;

    public Hub(string uri, HubSettingsViewModel parentVm)
    {
        Uri = uri;
        _parentVm = parentVm;
    }

    public void Remove()
    {
        _parentVm.HubList.Remove(this);
    }
}
