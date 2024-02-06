// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.SystemInterfaces;
using NUnit.Framework;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using Telerik.JustMock;

namespace NewRelic.Core.Tests.NewRelic.SystemInterfaces
{
    [TestFixture]
    public class DnsStaticTests
    {
        private const string DomainName = "Domain-Name";
        private const string EmptyDomainName = "";

        private INetworkData _mockNetworkData;
        private NetworkData _realNetworkData;

        [SetUp]
        public void Setup()
        {
            _mockNetworkData = Mock.Create<INetworkData>();
            _realNetworkData = new NetworkData();

        }

        [Test]
        public void GetFullHostName_Combines_HostNameAndPopulatedDomain_Correctly()
        {
            var dnsStatic = new DnsStatic(_mockNetworkData);
            var hostname = dnsStatic.GetHostName();
            Mock.Arrange(() => _mockNetworkData.GetDomainName(Arg.IsAny<INetworkInterfaceData>())).Returns(DomainName);
            var expectedHostname = $"{hostname}.{DomainName}";

            // ACT
            var actualHostname = dnsStatic.GetFullHostName();

            Assert.That(actualHostname, Is.EqualTo(expectedHostname));
        }

        [Test]
        public void GetFullHostName_Combines_HostNameAndEmptyDomain_Correctly()
        {
            var dnsStatic = new DnsStatic(_mockNetworkData);
            var expectedHostname = dnsStatic.GetHostName();
            Mock.Arrange(() => _mockNetworkData.GetDomainName(Arg.IsAny<INetworkInterfaceData>())).Returns(EmptyDomainName);

            // ACT
            var actualHostname = dnsStatic.GetFullHostName();

            Assert.That(actualHostname, Is.EqualTo(expectedHostname));
        }

        [Test]
        public void GetIpAddresses_Returns_OneAddress()
        {
            var dnsStatic = new DnsStatic(_realNetworkData);
            var correctIPAddress = IPAddress.Parse("1.0.0.0");
            var wrongIPAddress = IPAddress.Parse("2.0.0.0");
            var correctUnicastIPAddress = Mock.Create<UnicastIPAddressInformation>();
            var wrongUnicastIPAddress = Mock.Create<UnicastIPAddressInformation>();
            Mock.Arrange(() => correctUnicastIPAddress.Address).Returns(correctIPAddress);
            Mock.Arrange(() => wrongUnicastIPAddress.Address).Returns(wrongIPAddress);

            var map = new List<INetworkInterfaceData>();
            map.Add(new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation> { correctUnicastIPAddress }));
            map.Add(new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation> { wrongUnicastIPAddress }));

            // ACT
            var activeInterface = _realNetworkData.GetActiveNetworkInterface(correctIPAddress, map);
            var actualAddresses = dnsStatic.GetIpAddresses();

            Assert.That(actualAddresses, Has.Count.EqualTo(1));
            Assert.That(actualAddresses[0], Is.EqualTo(correctIPAddress.ToString()));
        }

        [Test]
        public void GetIpAddresses_Returns_TwoAddresses()
        {
            var dnsStatic = new DnsStatic(_realNetworkData);
            var correctIPAddress = IPAddress.Parse("1.0.0.0");
            var otherCorrectIPAddress = IPAddress.Parse("3.0.0.0");
            var wrongIPAddress = IPAddress.Parse("2.0.0.0");
            var correctUnicastIPAddress = Mock.Create<UnicastIPAddressInformation>();
            var otherCorrectUnicastIPAddress = Mock.Create<UnicastIPAddressInformation>();
            var wrongUnicastIPAddress = Mock.Create<UnicastIPAddressInformation>();
            Mock.Arrange(() => correctUnicastIPAddress.Address).Returns(correctIPAddress);
            Mock.Arrange(() => otherCorrectUnicastIPAddress.Address).Returns(otherCorrectIPAddress);
            Mock.Arrange(() => wrongUnicastIPAddress.Address).Returns(wrongIPAddress);

            var map = new List<INetworkInterfaceData>();
            map.Add(new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation> { correctUnicastIPAddress, otherCorrectUnicastIPAddress }));
            map.Add(new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation> { wrongUnicastIPAddress }));

            // ACT
            var activeInterface = _realNetworkData.GetActiveNetworkInterface(correctIPAddress, map);
            var actualAddresses = dnsStatic.GetIpAddresses();

            Assert.That(actualAddresses, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(actualAddresses[0], Is.EqualTo(correctIPAddress.ToString()));
                Assert.That(actualAddresses[1], Is.EqualTo(otherCorrectIPAddress.ToString()));
            });
        }

        [Test]
        public void GetIpAddresses_Returns_NoAddresses_When_GetNetworkInterfaceData_IsEmpty()
        {
            var dnsStatic = new DnsStatic(_realNetworkData);
            var ipAddress = IPAddress.Parse("1.0.0.0");
            var map = new List<INetworkInterfaceData>();

            // ACT
            var activeInterface = _realNetworkData.GetActiveNetworkInterface(ipAddress, map);
            var actualAddresses = dnsStatic.GetIpAddresses();

            Assert.That(actualAddresses, Is.Empty);
        }

        [Test]
        public void GetIpAddresses_Returns_TruncatedIPv6Address()
        {
            var dnsStatic = new DnsStatic(_realNetworkData);
            var expectedIPAddress = "fe80:1:2:3:86e:15a0:36d6:9b00";
            var scopeId = 66;
            var correctIPAddress = IPAddress.Parse(expectedIPAddress);
            correctIPAddress.ScopeId = scopeId;
            var wrongIPAddress = IPAddress.Parse("2.0.0.0");
            var correctUnicastIPAddress = Mock.Create<UnicastIPAddressInformation>();
            var wrongUnicastIPAddress = Mock.Create<UnicastIPAddressInformation>();
            Mock.Arrange(() => correctUnicastIPAddress.Address).Returns(correctIPAddress);
            Mock.Arrange(() => wrongUnicastIPAddress.Address).Returns(wrongIPAddress);

            var map = new List<INetworkInterfaceData>();
            map.Add(new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation> { correctUnicastIPAddress }));
            map.Add(new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation> { wrongUnicastIPAddress }));

            // ACT
            var activeInterface = _realNetworkData.GetActiveNetworkInterface(correctIPAddress, map);
            var actualAddresses = dnsStatic.GetIpAddresses();

            Assert.That(actualAddresses, Has.Count.EqualTo(1));
            Assert.That(actualAddresses[0], Is.EqualTo(expectedIPAddress));
        }
    }
}
