using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Configuration
{
    public class RunTimeConfiguration
    {
        public IEnumerable<String> ApplicationNames;

        public RunTimeConfiguration()
        {
            ApplicationNames = Enumerable.Empty<String>();
        }

        public RunTimeConfiguration(IEnumerable<String> applicationNames)
        {
            ApplicationNames = applicationNames.ToList();
        }
    }
}
