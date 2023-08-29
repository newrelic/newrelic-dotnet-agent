// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NUnit.Framework;
using Telerik.JustMock;
using Telerik.JustMock.Helpers;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    internal class NRHttpClientFactoryTests
    {
        private IConfiguration _mockConfiguration;
        private IWebProxy _mockProxy;
        private IHttpClientFactory _httpClientFactory;

        [SetUp]
        public void SetUp()
        {
            _mockConfiguration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _mockConfiguration.AgentLicenseKey).Returns("12345");
            Mock.Arrange(() => _mockConfiguration.AgentRunId).Returns("123");
            Mock.Arrange(() => _mockConfiguration.CollectorMaxPayloadSizeInBytes).Returns(int.MaxValue);

            _mockProxy = Mock.Create<IWebProxy>();

            _httpClientFactory = new NRHttpClientFactory();
        }

        [Test]
        public void CreateClient_NotNull()
        {
            var client = _httpClientFactory.CreateClient(null, _mockConfiguration);

            Assert.That(client, Is.Not.Null);
        }

        [Test]
        public void CreateClient_NoProxy_ReturnsSameClient()
        {
            var clientA = _httpClientFactory.CreateClient(null, _mockConfiguration);
            var clientB = _httpClientFactory.CreateClient(null, _mockConfiguration);

            Assert.That(clientA == clientB);
        }

        [Test]
        public void CreateClient_Proxy_ReturnsSameClient()
        {
            var clientA = _httpClientFactory.CreateClient(_mockProxy, _mockConfiguration);
            var clientB = _httpClientFactory.CreateClient(_mockProxy, _mockConfiguration);

            Assert.That(clientA == clientB);
        }

        [Test]
        public void CreateClient_NoProxyToProxy_ReturnsNewClient()
        {
            var clientA = _httpClientFactory.CreateClient(null, _mockConfiguration);
            var clientB = _httpClientFactory.CreateClient(_mockProxy, _mockConfiguration);

            Assert.That(clientA != clientB);
        }

        [Test]
        public void CreateClient_ProxyToNoProxy_ReturnsNewClient()
        {
            var clientA = _httpClientFactory.CreateClient(_mockProxy, _mockConfiguration);
            var clientB = _httpClientFactory.CreateClient(null, _mockConfiguration);

            Assert.That(clientA != clientB);
        }
    }
}
#endif
