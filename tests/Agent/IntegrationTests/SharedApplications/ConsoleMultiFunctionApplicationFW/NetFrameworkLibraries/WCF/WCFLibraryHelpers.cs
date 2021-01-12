// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using System;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF
{
    public static class WCFLibraryHelpers
    {
        public static Uri GetEndpointAddress(WCFBindingType bindingTypeEnum, int port, string relativeUrl)
        {
            relativeUrl = relativeUrl.TrimStart('/');
            switch (bindingTypeEnum)
            {
                case WCFBindingType.BasicHttp:
                case WCFBindingType.WSHttp:
                case WCFBindingType.WebHttp:
                case WCFBindingType.Custom:
                case WCFBindingType.CustomClass:
                    return new Uri($@"http://localhost:{port}/{relativeUrl}");
                case WCFBindingType.NetTcp:
                    return new Uri($@"net.tcp://localhost:{port}/{relativeUrl}");
                default:
                    throw new NotSupportedException($"Binding Type {bindingTypeEnum}");
            }
        }

        public static Binding GetCustomBinding(string configurationName = null)
        {
            if (string.IsNullOrEmpty(configurationName))
            {
                var httpTransport = new HttpTransportBindingElement
                {
                    AuthenticationScheme = System.Net.AuthenticationSchemes.Anonymous,
                    HostNameComparisonMode = HostNameComparisonMode.StrongWildcard
                };

                return new CustomBinding(httpTransport);
            }
            else if (configurationName == "CustomWcfBinding")
            {
                return new CustomClassBinding();
            }

            return new CustomBinding(configurationName);
        }

        public static void StartAgentWithExternalCall()
        {
            // start up the agent before the client is initialized since this can happen prior to connect.
            // Tried StartAgent from the API, but that still resulted in flickers, due I beleive to the call completing fast.
            // Reaching out to google takes a bit of time and give the agent more time to spin up.
            try
            {
                var wc = new WebClient();
                var s = wc.DownloadString(new Uri("https://www.google.com/"));
                var l = s.Length;
            }
            catch
            {
                //do nothing and prevent an error from occurring.
            }
        }
    }
}
