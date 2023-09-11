// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF
{
    [Library]
    public class WCFServiceIISHosted
    {
        private HostedWebCore _hostedWebCore;
        private static readonly Dictionary<WCFBindingType, string> _bindingTypeNames = new Dictionary<WCFBindingType, string>
        {
            { WCFBindingType.BasicHttp, "basicHttpBinding" },
            { WCFBindingType.WSHttp, "wsHttpBinding" },
            { WCFBindingType.WSHttpUnsecure, "wsHttpBinding" },
            { WCFBindingType.WebHttp, "webHttpBinding" },
            { WCFBindingType.NetTcp, "netTcpBinding" },
            { WCFBindingType.Custom, "CustomHttpBinding" }
        };

        [LibraryMethod]
        public void StartService(string srcAppPath, string bindingType, int port, string relativePath, bool aspNetCompatibilityEnabled)
        {
            WCFLibraryHelpers.StartAgentWithExternalCall();
            var bindingTypeEnum = (WCFBindingType)Enum.Parse(typeof(WCFBindingType), bindingType, true);
            _hostedWebCore = new HostedWebCore();
            _hostedWebCore.StageWebApp(srcAppPath, port);
            ConfigureBinding(bindingTypeEnum, relativePath, port);
            ConfigureASPNetCompatibilityMode(aspNetCompatibilityEnabled);
            _hostedWebCore.Start();
        }


        private void ConfigureASPNetCompatibilityMode(bool enabled)
        {
            //Clean out any bindings that are in the Web.Config
            XmlUtils.DeleteXmlNode(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel" }, "serviceHostingEnvironment");

            XmlUtils.AddXmlNode(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel" }, "serviceHostingEnvironment", string.Empty);

            XmlUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "serviceHostingEnvironment" },
                "aspNetCompatibilityEnabled", enabled.ToString().ToLower());

            XmlUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "serviceHostingEnvironment" },
                "multipleSiteBindingsEnabled", true.ToString().ToLower());
        }

        private void ConfigureBinding(WCFBindingType bindingType, string relativePath, int port)
        {
            //Clean out any bindings that are in the Web.Config
            XmlUtils.DeleteXmlNode(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel" }, "services");

            //services node
            XmlUtils.AddXmlNode(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel" }, "services", string.Empty);

            //services/service/Name
            XmlUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "services", "service" },
                "name", "NewRelic.Agent.IntegrationTests.Shared.Wcf.WcfService");

            //services/service/endpoint - Address
            XmlUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "services", "service", "endpoint" },
                "address", relativePath);

            //services/service/endpoint - contract
            XmlUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "services", "service", "endpoint" },
                "contract", "IWcfService");

            //services/service/endpoint - binding
            XmlUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "services", "service", "endpoint" },
                "binding", _bindingTypeNames[bindingType]);

            if (bindingType == WCFBindingType.WSHttpUnsecure)
            {
                XmlUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                    new[] { "configuration", "system.serviceModel", "services", "service", "endpoint" },
                    "bindingConfiguration", bindingType.ToString());
            }

            if (bindingType == WCFBindingType.WebHttp)
            {
                XmlUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "services", "service", "endpoint" },
                "behaviorConfiguration", "webHttpBehavior");
            }
        }

        /// <summary>
        /// Stops the WCF Service
        /// </summary>
        [LibraryMethod]
        public void StopService()
        {
            _hostedWebCore?.Stop();
            ConsoleMFLogger.Info("Delay on stop of Hosted Web Core (to allow time for logging)");
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}
