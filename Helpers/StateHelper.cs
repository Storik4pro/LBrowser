using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinesBrowser.Helpers
{
    public class StateHelper
    {
        private static StateHelper _instance;
        private static readonly object _lock = new object();
        public static StateHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new StateHelper();
                    return _instance;
                }
            }
        }
        public readonly string RecommendedServerVersion = "1.0.1.0";
        public string ServerVersion { get; set; } = "UNKNOWN";
        public List<string> AvailableFeatures { get; set; }
        public List<long> OpenedTabsId { get; set; } = new List<long>();
    }
}
