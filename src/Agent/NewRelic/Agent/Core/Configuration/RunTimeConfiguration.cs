using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Configuration
{
    public class RunTimeConfiguration
    {
        public IEnumerable<string> ApplicationNames;

        public RunTimeConfiguration()
        {
            ApplicationNames = Enumerable.Empty<string>();
        }

        public RunTimeConfiguration(IEnumerable<string> applicationNames)
        {
            ApplicationNames = applicationNames.ToList();
        }
    }
}
