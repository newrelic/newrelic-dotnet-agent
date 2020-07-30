/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
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
