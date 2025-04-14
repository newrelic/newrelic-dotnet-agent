// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public class NewRelicMetadataEnvTests : NewRelicIntegrationTest<RemoteServiceFixtures.AgentApiExecutor>
    {
        private readonly RemoteServiceFixtures.AgentApiExecutor _fixture;
        private readonly Dictionary<string, string> _envs = new Dictionary<string, string>
        {
                { "NEW_RELIC_METADATA_KUBERNETES_CLUSTER_NAME", "fsi" },
                { "NEW_RELIC_METADATA_KUBERNETES_NODE_NAME", "nodea" },
                { "NEW_RELIC_METADATA_KUBERNETES_NAMESPACE_NAME", "default" },
                { "NEW_RELIC_METADATA_KUBERNETES_POD_NAME", "10.0.0.1" },
                { "NEW_RELIC_METADATA_KUBERNETES_CONTAINER_NAME", "busybox" }
        };

        public NewRelicMetadataEnvTests(RemoteServiceFixtures.AgentApiExecutor fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.SetAdditionalEnvironmentVariables(_envs);
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metadata = _fixture.AgentLog.GetConnectData().Metadata;
            foreach (var kp in _envs)
            {
                Assert.Contains(kp.Key, (IDictionary<string, string>)metadata);
                Assert.Equal(kp.Value, metadata[kp.Key]);
            }
        }
    }
}
