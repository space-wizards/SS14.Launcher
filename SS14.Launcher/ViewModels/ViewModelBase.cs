using System.ComponentModel;
using ReactiveUI;

namespace SS14.Launcher.ViewModels;

public class ViewModelBase : ReactiveObject, IViewModelBase
{
    protected void OnPropertyChanged(PropertyChangedEventArgs e) => this.RaisePropertyChanged(e.PropertyName);
    protected void OnPropertyChanging(PropertyChangingEventArgs e) => this.RaisePropertyChanging(e.PropertyName);

    protected void OnPropertyChanged(string name) => this.RaisePropertyChanged(name);
    protected void OnPropertyChanging(string name) => this.RaisePropertyChanging(name);
}

/// <summary>
/// Signifies to <see cref="ViewLocator"/> that this viewmodel can be automatically located.
/// </summary>
public interface IViewModelBase
{
}
