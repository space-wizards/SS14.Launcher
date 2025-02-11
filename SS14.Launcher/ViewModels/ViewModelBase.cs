using System.ComponentModel;
using ReactiveUI;

namespace SS14.Launcher.ViewModels;

public class ViewModelBase : ReactiveObject, IViewModelBase
{
    protected void OnPropertyChanged(PropertyChangedEventArgs e) => this.RaisePropertyChanged(e.PropertyName);
    protected void OnPropertyChanging(PropertyChangingEventArgs e) => this.RaisePropertyChanging(e.PropertyName);
}

/// <summary>
/// Signifies to <see cref="ViewLocator"/> that this viewmodel can be automatically located.
/// </summary>
public interface IViewModelBase
{
}
