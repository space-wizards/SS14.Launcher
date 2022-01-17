using System.Diagnostics;
using Splat;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class OptionsTabViewModel : MainWindowTabViewModel
{
    public DataManager Cfg { get; }
    private readonly IEngineManager _engineManager;
    private readonly ContentManager _contentManager;

    public OptionsTabViewModel()
    {
        Cfg = Locator.Current.GetRequiredService<DataManager>();
        _engineManager = Locator.Current.GetRequiredService<IEngineManager>();
        _contentManager = Locator.Current.GetRequiredService<ContentManager>();
        _contentManager.ClearAll();
    }

#if RELEASE
        public bool HideDisableSigning => true;
#else
    public bool HideDisableSigning => false;
#endif

    public override string Name => "Options";

    public bool CompatMode
    {
        get => Cfg.GetCVar(CVars.CompatMode);
        set
        {
            Cfg.SetCVar(CVars.CompatMode, value);
            Cfg.CommitConfig();
        }
    }

    public bool DynamicPgo
    {
        get => Cfg.GetCVar(CVars.DynamicPgo);
        set
        {
            Cfg.SetCVar(CVars.DynamicPgo, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogClient
    {
        get => Cfg.GetCVar(CVars.LogClient);
        set
        {
            Cfg.SetCVar(CVars.LogClient, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogLauncher
    {
        get => Cfg.GetCVar(CVars.LogLauncher);
        set
        {
            Cfg.SetCVar(CVars.LogLauncher, value);
            Cfg.CommitConfig();
        }
    }

    public bool DisableSigning
    {
        get => Cfg.GetCVar(CVars.DisableSigning);
        set
        {
            Cfg.SetCVar(CVars.DisableSigning, value);
            Cfg.CommitConfig();
        }
    }

    public void ClearEngines()
    {
        _engineManager.ClearAllEngines();
    }

    public void ClearServerContent()
    {
        /*foreach (var content in Cfg.ServerContent.Items.ToArray())
        {
            Cfg.RemoveInstallation(content);
        }

        foreach (var file in Directory.EnumerateFiles(LauncherPaths.DirServerContent))
        {
            File.Delete(file);
        }

        Cfg.CommitConfig();*/
    }

    public void OpenLogDirectory()
    {
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = LauncherPaths.DirLogs
        });
    }
}
