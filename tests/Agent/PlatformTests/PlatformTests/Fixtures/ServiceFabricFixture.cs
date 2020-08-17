// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Net;
using System.Threading;
using NewRelic.Agent.IntegrationTests.Shared;
using PlatformTests.Applications;

namespace PlatformTests.Fixtures
{
    public class ServiceFabricFixture : BaseFixture
    {
        private const string TestSettingCategory = "ServiceFabricTests";

        public ServiceFabricFixture() : base(new ServiceFabricApplication("ServiceFabricApplication", new[] { "OwinService" }, TestSettingCategory))
        {
        }

        public void WarmUp()
        {
            var maxTry = 3;
            for (var i = 1; i <= maxTry; i++)
            {
                try
                {
                    var address = $@"{Application.TestConfiguration["TestUrl"]}/api/";
                    var request = (HttpWebRequest)WebRequest.Create(address);
                    request.Timeout = 180000;
                    var response = request.GetResponse();
                    break;
                }
                catch
                {
                    if (i == maxTry)
                        throw;

                    Thread.Sleep(10000);
                }
            }
        }

        public string GetAgentLog()
        {
            var address = $@"{Application.TestConfiguration["TestUrl"]}/api/Logs/AgentLog";
            var request = (HttpWebRequest)WebRequest.Create(address);
            request.Timeout = 300000;
            var response = request.GetResponse();
            var stream = new StreamReader(response.GetResponseStream());

            var agentLogString = stream.ReadToEnd();

            agentLogString = agentLogString.TrimStart('"').TrimEnd('"');
            agentLogString = System.Text.RegularExpressions.Regex.Unescape(agentLogString);

            return agentLogString;
        }
    }
}
