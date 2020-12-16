// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.ApplicationLibraries.Wcf;
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
            CommonUtils.DeleteXmlNode(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel" }, "serviceHostingEnvironment");

            CommonUtils.AddXmlNode(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel" }, "serviceHostingEnvironment", string.Empty);

            CommonUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "serviceHostingEnvironment" },
                "aspNetCompatibilityEnabled", enabled.ToString().ToLower());

            CommonUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "serviceHostingEnvironment" },
                "multipleSiteBindingsEnabled", true.ToString().ToLower());
        }

        private void ConfigureBinding(WCFBindingType bindingType, string relativePath, int port)
        {
            //	<services>
            //		<service name="NewRelic.Agent.IntegrationTestHelpers.ApplicationLibraries.Wcf.WcfService">
            //			<endpoint address="___relativePath___" contract="IWcfService" binding="___bindingType___">
            //			</endpoint>
            //	 </service>
            //	</services>

            //Clean out any bindings that are in the Web.Config
            CommonUtils.DeleteXmlNode(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel" }, "services");

            //services node
            CommonUtils.AddXmlNode(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel" }, "services", string.Empty);

            //services/service/Name
            CommonUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "services", "service" },
                "name", "NewRelic.Agent.IntegrationTestHelpers.ApplicationLibraries.Wcf.WcfService");

            //services/service/endpoint - Address
            CommonUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "services", "service", "endpoint" },
                "address", relativePath);

            //services/service/endpoint - contract
            CommonUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "services", "service", "endpoint" },
                "contract", "IWcfService");

            //services/service/endpoint - binding
            CommonUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
                new[] { "configuration", "system.serviceModel", "services", "service", "endpoint" },
                "binding", _bindingTypeNames[bindingType]);

            if (bindingType == WCFBindingType.WebHttp)
            {
                CommonUtils.ModifyOrCreateXmlAttribute(_hostedWebCore.WebConfigPath, "",
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
            Logger.Info("Delay on stop of Hosted Web Core (to allow time for logging)");
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}
