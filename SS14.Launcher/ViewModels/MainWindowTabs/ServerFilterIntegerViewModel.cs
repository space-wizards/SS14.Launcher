using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS14.Launcher.ViewModels.MainWindowTabs
{
    public sealed class ServerFilterIntegerViewModel: ServerFilterBaseViewModel 
    {
        public int? Data
        {
            get
            {
                bool isVal = int.TryParse(Filter.Data, out var val);
                return isVal ? val : null;
            }

            set
            {
                var filter_val = value is not null ? value.ToString() : string.Empty;
                ServerFilter filter = new ServerFilter(Filter.Category, filter_val);
                _parent.ReplaceFilter(filter, Filter);
                Filter = filter;
            }
        }

        public int? Maximum { get; }
        public int? Minimum { get; }
        public int Increment { get; }


        public ServerFilterIntegerViewModel(
            string name,
            string shortName,
            ServerFilter filter,
            int increment,
            ServerListFiltersViewModel parent,
            int? max = null,
            int? min = null) : base(name, shortName, filter, parent)
        {
            Maximum = max;
            Minimum = min;
            Increment = increment;
        }
    }
}
