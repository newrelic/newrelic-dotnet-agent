// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Net.Http;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class DTBasicMVCApplicationFixture : RemoteApplicationFixture
    {
        private const string DTHeaderName = "NewRelic";

        public DTBasicMVCApplicationFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Bounded))
        {
        }

        #region Payload Tests

        public void Initiate()
        {
            var address = $"http://{DestinationServerName}:{Port}/DistributedTracing/Initiate";

            GetStringAndIgnoreResult(address);
        }

        public void ReceiveDTPayload()
        {
            var address = $"http://{DestinationServerName}:{Port}/DistributedTracing/ReceivePayload";

            //string payload = "eyJ2IjpbMCwxXSwiZCI6eyJ0eSI6IkFwcCIsImFjIjoiMSIsImFwIjoiNTE0MjQiLCJwYSI6IjVmYTNjMDE0OThlMjQ0YTYiLCJpZCI6IjI3ODU2ZjcwZDNkMzE0YjciLCJ0ciI6IjMyMjFiZjA5YWEwYmNmMGQiLCJwciI6MC4xMjM0LCJzYSI6ZmFsc2UsInRpIjoxNDgyOTU5NTI1NTc3fX0=";
            string payload =
                "eyJ2IjpbMCwxXSwiZCI6eyJ0eSI6IkFwcCIsImFjIjoiMSIsImFwIjoiNTE0MjQiLCJwYSI6IjVmYTNjMDE0OThlMjQ0YTYiLCJpZCI6IjI3ODU2ZjcwZDNkMzE0YjciLCJ0ciI6IjMyMjFiZjA5YWEwYmNmMGQiLCJwciI6MC4xMjM0LCJzYSI6ZmFsc2UsInRpIjoxNDgyOTU5NTI1NTc3LCAidHgiOiAiMjc4NTZmNzBkM2QzMTRiNyJ9fQ==";

            var headers = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(DTHeaderName, payload) };
            GetStringAndIgnoreResult(address, headers);
        }

        #endregion

        #region Supportability Metric Tests

        public void GenerateMajorVersionMetric()
        {
            var address = $"http://{DestinationServerName}:{Port}/DistributedTracing/SupportabilityReceivePayload";

            // Version: [9999,1]
            string payload = "eyJ2IjpbOTk5OSwxXSwiZCI6eyJ0eSI6IkFwcCIsImFjIjoiOTEyMyIsImFwIjoiNTE0MjQiLCJpZCI6IjI3ODU2ZjcwZDNkMzE0YjciLCJ0ciI6IjE0ODI5NTk1MjU1NzciLCJwciI6MC4xMjM0LCJzYSI6ZmFsc2UsInRpIjoxNTI5NDI0MTMwNjAzLCJwYSI6IjVmYTNjMDE0OThlMjQ0YTYifX0=";

            var headers = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(DTHeaderName, payload) };
            GetStringAndIgnoreResult(address, headers);
        }

        public void GenerateIgnoredNullMetric()
        {
            var address = $"http://{DestinationServerName}:{Port}/DistributedTracing/SupportabilityReceivePayload";
            string payload = null;

            var headers = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(DTHeaderName, payload) };
            GetStringAndIgnoreResult(address, headers);
        }

        public void GenerateParsePayloadMetric()
        {
            var address = $"http://{DestinationServerName}:{Port}/DistributedTracing/SupportabilityReceivePayload";
            string payload = "eyJ2IjpbMCwxXSwiZCI6eyJ0eSI6IkFwcCIsImZvbyI6ImJhciIsYWMiOiI5MTIzIiwiYXAiOiI1MTQyNCIsImlkIjoiMjc4NTZmNzBkM2QzMTRiNyIsInRyIjoiMTQ4Mjk1OTUyNTU3NyIsInByIjowLjEyMzQsInNhIjpmYWxzZSwidGkiOjE1Mjk0MjQxMzA2MDMsInBhIjoiNWZhM2MwMTQ5OGUyNDRhNiJ9fQ==";

            var headers = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(DTHeaderName, payload) };
            GetStringAndIgnoreResult(address, headers);
        }

        public void GenerateAcceptSuccessMetric()
        {
            var address = $"http://{DestinationServerName}:{Port}/DistributedTracing/SupportabilityReceivePayload";
            string payload = "eyJ2IjpbMCwxXSwiZCI6eyJ0eSI6IkFwcCIsImFjIjoiMSIsImFwIjoiNTE0MjQiLCJwYSI6IjVmYTNjMDE0OThlMjQ0YTYiLCJpZCI6IjI3ODU2ZjcwZDNkMzE0YjciLCJ0ciI6IjMyMjFiZjA5YWEwYmNmMGQiLCJwciI6MC4xMjM0LCJzYSI6ZmFsc2UsInRpIjoxNDgyOTU5NTI1NTc3fX0=";

            var headers = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(DTHeaderName, payload) };
            GetStringAndIgnoreResult(address, headers);
        }

        public void GenerateUntrustedAccountMetric()
        {
            var address = $"http://{DestinationServerName}:{Port}/DistributedTracing/SupportabilityReceivePayload";

            //{"v":[0,1],"d":{"ty":"App","ac":"8675309","ap":"51424","pa":"5fa3c01498e244a6","id":"27856f70d3d314b7","tr":"3221bf09aa0bcf0d","pr":0.1234,"sa":false,"ti":1482959525577}}
            string payload = "eyJ2IjpbMCwxXSwiZCI6eyJ0eSI6IkFwcCIsImFjIjoiODY3NTMwOSIsImFwIjoiNTE0MjQiLCJwYSI6IjVmYTNjMDE0OThlMjQ0YTYiLCJpZCI6IjI3ODU2ZjcwZDNkMzE0YjciLCJ0ciI6IjMyMjFiZjA5YWEwYmNmMGQiLCJwciI6MC4xMjM0LCJzYSI6ZmFsc2UsInRpIjoxNDgyOTU5NTI1NTc3fX0=";

            var headers = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(DTHeaderName, payload) };
            GetStringAndIgnoreResult(address, headers);
        }

        public void GenerateCreateSuccessMetric()
        {
            var address = $"http://{DestinationServerName}:{Port}/DistributedTracing/SupportabilityCreatePayload";
            string payload = "eyJ2IjpbMCwxXSwiZCI6eyJ0eSI6IkFwcCIsImFjIjoiMSIsImFwIjoiNTE0MjQiLCJwYSI6IjVmYTNjMDE0OThlMjQ0YTYiLCJpZCI6IjI3ODU2ZjcwZDNkMzE0YjciLCJ0ciI6IjMyMjFiZjA5YWEwYmNmMGQiLCJwciI6MC4xMjM0LCJzYSI6ZmFsc2UsInRpIjoxNDgyOTU5NTI1NTc3fX0=";

            var headers = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(DTHeaderName, payload) };
            GetStringAndIgnoreResult(address, headers);
        }
        #endregion
    }
}
