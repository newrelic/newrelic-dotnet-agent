// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class OwinRemotingFixture : RemoteApplicationFixture
    {
        private const string ServerApplicationDirectoryName = @"OwinRemotingServer";
        private const string ServerExecutableName = @"OwinRemotingServer.exe";
        private const string ClientApplicationDirectoryName = @"OwinRemotingClient";
        private const string ClientExecutableName = @"OwinRemotingClient.exe";
        internal RemoteService OwinRemotingServerApplication { get; set; }

        public OwinRemotingFixture() : base(new RemoteService(ClientApplicationDirectoryName, ClientExecutableName, ApplicationType.Bounded))
        {
            var environmentVariables = new Dictionary<string, string>();

            OwinRemotingServerApplication = new RemoteService(ServerApplicationDirectoryName, ServerExecutableName, ApplicationType.Bounded);
            OwinRemotingServerApplication.CopyToRemote();
            OwinRemotingServerApplication.Start(string.Empty, environmentVariables, captureStandardOutput: false, doProfile: false);
        }

        public string GetObjectTcp()
        {
            var address = string.Format(@"http://{0}:{1}/Remote/GetObjectTcp", DestinationServerName, Port);
            var result = GetStringAndAssertEqual(address, null);

            return result;
        }

        public string GetObjectHttp()
        {
            var address = string.Format(@"http://{0}:{1}/Remote/GetObjectHttp", DestinationServerName, Port);
            var result = GetStringAndAssertEqual(address, null);

            return result;
        }

        public override void Dispose()
        {
            OwinRemotingServerApplication.Shutdown(true);
            OwinRemotingServerApplication.Dispose();
            base.Dispose();
        }
    }
}
