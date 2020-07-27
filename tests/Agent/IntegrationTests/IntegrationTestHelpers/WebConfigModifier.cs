using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class WebConfigModifier
    {
        private readonly string _configFilePath;

        public WebConfigModifier(string configFilePath)
        {
            _configFilePath = configFilePath;
        }

        public void ForceLegacyAspPipeline()
        {
            CommonUtils.DeleteXmlNode(_configFilePath, "", new[] { "configuration", "system.web" }, "httpRuntime");
        }
    }
}
