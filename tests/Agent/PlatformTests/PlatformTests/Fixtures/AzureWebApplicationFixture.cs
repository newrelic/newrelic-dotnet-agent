/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.IO;
using System.Net;
using System.Threading;
using PlatformTests.Applications;

namespace PlatformTests.Fixtures
{
    public class AzureWebApplicationFixture : BaseFixture
    {
        public const string TestSettingCategory = "AzureWebApplicationTests";

        public AzureWebApplicationFixture() : base(new AzureWebApplication("NetFrameworkBasicApplication", TestSettingCategory))
        {
        }

        public void WarmUp()
        {
            var maxTry = 3;
            for (var i = 1; i <= maxTry; i++)
            {
                try
                {
                    var address = Application.TestConfiguration["TestUrl"];
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
            var address = Application.TestConfiguration["TestUrl"] + "/api/Logs/AgentLogWithTransactionSample";
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
