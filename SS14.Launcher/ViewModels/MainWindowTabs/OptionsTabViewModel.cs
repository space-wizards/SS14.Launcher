using System;
using ReactiveUI;
using SS14.Launcher.Models;
using SS14.Launcher.Views;

namespace SS14.Launcher.ViewModels.MainWindowTabs
{
    public class OptionsTabViewModel : MainWindowTabViewModel
    {
        public readonly ConfigurationManager Cfg;

        public OptionsTabViewModel(ConfigurationManager cfg)
        {
            Cfg = cfg;
        }

        public bool ForceGLES2
        {
            get
            {
                return Cfg.ForceGLES2;
            }
            set
            {
                Cfg.ForceGLES2 = value;
            }
        }

        public override string Name => "Options";
    }
}
