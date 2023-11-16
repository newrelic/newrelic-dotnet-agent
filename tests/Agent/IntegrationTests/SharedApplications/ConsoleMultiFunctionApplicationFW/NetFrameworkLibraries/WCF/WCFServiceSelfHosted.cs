// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF
{
    [Library]
    public class WCFServiceSelfHosted
    {
        private ServiceHost _wcfService_SelfHosted;

        /// <summary>
        /// Starts the WCF Service with a specific binding and port
        /// </summary>
        /// <param name="bindingType"></param>
        /// <param name="port"></param>
        [LibraryMethod]
        public void StartService(string bindingType, int port, string relativePath)
        {
            //Debugger.Launch();

            relativePath = relativePath.TrimStart('/');

            if (_wcfService_SelfHosted != null)
            {
                StopService();
            }

            WCFLibraryHelpers.StartAgentWithExternalCall();
            var bindingTypeEnum = (WCFBindingType)Enum.Parse(typeof(WCFBindingType), bindingType, true);
            var baseAddress = WCFLibraryHelpers.GetEndpointAddress(bindingTypeEnum, port, relativePath);
            ConsoleMFLogger.Info($"Starting WCF Service using {bindingTypeEnum} binding at endpoint {baseAddress}");
            _wcfService_SelfHosted = new ServiceHost(typeof(WcfService), baseAddress);
            if (bindingTypeEnum != WCFBindingType.NetTcp)
            {
                var smb = new ServiceMetadataBehavior();
                smb.HttpGetEnabled = true;
                smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
                _wcfService_SelfHosted.Description.Behaviors.Add(smb);
            }

            switch (bindingTypeEnum)
            {
                case WCFBindingType.BasicHttp:
                    _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), new BasicHttpBinding(), baseAddress);
                    break;
                case WCFBindingType.WebHttp:
                    var endpoint = _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), new WebHttpBinding(), baseAddress);
                    var behavior = new WebHttpBehavior();
                    endpoint.EndpointBehaviors.Add(behavior);
                    break;
                case WCFBindingType.WSHttp:
                    _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), new WSHttpBinding(), baseAddress);
                    break;
                case WCFBindingType.NetTcp:
                    _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), new NetTcpBinding(), baseAddress);
                    break;
                case WCFBindingType.Custom:
                    _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), WCFLibraryHelpers.GetCustomBinding(), baseAddress);
                    break;
                case WCFBindingType.CustomClass:
                    _wcfService_SelfHosted.AddServiceEndpoint(typeof(IWcfService), WCFLibraryHelpers.GetCustomBinding("CustomWcfBinding"), baseAddress);
                    break;
                default:
                    throw new NotImplementedException($"Binding Type {bindingTypeEnum}");
            }

            _wcfService_SelfHosted.Open();
        }

        /// <summary>
        /// Stops the WCF Service
        /// </summary>
        [LibraryMethod]
        public void StopService()
        {
            _wcfService_SelfHosted?.Close();
        }

    }
}
