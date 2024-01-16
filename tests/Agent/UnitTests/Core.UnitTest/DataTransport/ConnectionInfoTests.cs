// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class ConnectionInfoTests
    {
        [Test]
        public void check_for_connectioninfo_proxy_uripath()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ProxyHost).Returns("https://hostname.test");
            Mock.Arrange(() => configuration.ProxyUriPath).Returns("path/htap.aspx");
            Mock.Arrange(() => configuration.ProxyPort).Returns(12345);

            var connectionInfo = new ConnectionInfo(configuration);

            ClassicAssert.AreEqual("https://hostname.test", connectionInfo.ProxyHost);
            ClassicAssert.AreEqual("path/htap.aspx", connectionInfo.ProxyUriPath);
            ClassicAssert.AreEqual(12345, connectionInfo.ProxyPort);
        }

        [Test]
        public void check_for_connectioninfo_proxy_uri_with_proxy_uripath()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ProxyHost).Returns("https://hostname.test");
            Mock.Arrange(() => configuration.ProxyUriPath).Returns("path/htap.aspx");
            Mock.Arrange(() => configuration.ProxyPort).Returns(12345);

            var connectionInfo = new ConnectionInfo(configuration);

            ClassicAssert.AreEqual("https://hostname.test:12345/path/htap.aspx", connectionInfo.Proxy.Address.ToString());
        }

        [Test]
        public void check_for_connectioninfo_proxy_uri_without_proxy_uripath()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ProxyHost).Returns("https://hostname.test");
            Mock.Arrange(() => configuration.ProxyPort).Returns(12345);

            var connectionInfo = new ConnectionInfo(configuration);

            ClassicAssert.AreEqual("https://hostname.test:12345/", connectionInfo.Proxy.Address.ToString());
        }

        [Test]
        public void check_for_connectioninfo_proxy_uripath_remove_leading_slash()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.ProxyHost).Returns("https://hostname.test");
            Mock.Arrange(() => configuration.ProxyUriPath).Returns("/path/htap.aspx");
            Mock.Arrange(() => configuration.ProxyPort).Returns(12345);

            var connectionInfo = new ConnectionInfo(configuration);

            ClassicAssert.AreEqual("https://hostname.test:12345/path/htap.aspx", connectionInfo.Proxy.Address.ToString());
        }
    }
}
