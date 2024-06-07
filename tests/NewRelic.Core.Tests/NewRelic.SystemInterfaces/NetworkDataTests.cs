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
    public class NetworkDataTests
    {
        private const string DomainName = "Domain-Name";

        private NetworkData _realNetworkData;
        private NetworkData _mockedNetworkData;

        [SetUp]
        public void Setup()
        {
            _realNetworkData = new NetworkData();
            _mockedNetworkData = Mock.Create<NetworkData>();  // need to mock class not interface to allow CallOriginal to function.
        }

        [Test]
        public void DomainName_Returns_Successfully_WithDomainName()
        {
            var networkInterface = new NetworkInterfaceData(DomainName, new List<UnicastIPAddressInformation>());

            // ACT
            var actualDomainName = _realNetworkData.GetDomainName(networkInterface);

            Assert.That(actualDomainName, Is.Not.Null);
            Assert.That(actualDomainName, Is.EqualTo(DomainName));
        }

        [Test]
        public void DomainName_Returns_Successfully_WithEmptyDomainName()
        {
            var networkInterface = new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation>());

            // ACT
            var actualDomainName = _realNetworkData.GetDomainName(networkInterface);

            Assert.That(actualDomainName, Is.Not.Null);
            Assert.That(actualDomainName, Is.EqualTo(string.Empty));
        }

        [Test]
        public void GetLocalIPAddress_Returns_Successfully()
        {
            // ACT
            var ipAddress = _realNetworkData.GetLocalIPAddress();

            Assert.That(ipAddress, Is.Not.Null);
            Assert.That(ipAddress, Is.Not.EqualTo(IPAddress.None));
        }

        [Test]
        public void IsActiveInterface_Returns_CorrectInterface()
        {
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
            var unicastIPAddressEnumerable = (IList<UnicastIPAddressInformation>)activeInterface.UnicastIPAddresses;

            Assert.That(activeInterface, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(activeInterface.UnicastIPAddresses, Has.Count.EqualTo(1));
                Assert.That(unicastIPAddressEnumerable[0].Address.ToString(), Is.EqualTo(correctIPAddress.ToString()));
            });
        }

        [Test]
        public void IsActiveInterface_Returns_EmptyInterface()
        {
            var firstIPAddress = IPAddress.Parse("1.0.0.0");
            var secondIPAddress = IPAddress.Parse("2.0.0.0");
            var localIPAddress = IPAddress.Parse("3.0.0.0");
            var firstUnicastIPAddress = Mock.Create<UnicastIPAddressInformation>();
            var secondUnicastIPAddress = Mock.Create<UnicastIPAddressInformation>();
            Mock.Arrange(() => firstUnicastIPAddress.Address).Returns(firstIPAddress);
            Mock.Arrange(() => secondUnicastIPAddress.Address).Returns(secondIPAddress);

            var map = new List<INetworkInterfaceData>();
            map.Add(new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation> { firstUnicastIPAddress }));
            map.Add(new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation> { secondUnicastIPAddress }));

            // ACT
            var activeInterface = _realNetworkData.GetActiveNetworkInterface(localIPAddress, map);
            var unicastIPAddressEnumerable = (IList<UnicastIPAddressInformation>)activeInterface.UnicastIPAddresses;

            Assert.That(activeInterface, Is.Not.Null);
            Assert.That(activeInterface.UnicastIPAddresses, Is.Empty);
        }

        [Test]
        public void IsActiveInterface_Returns_OriginalInterface_WhenGivenNewData()
        {
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
            var wrongInterface = _realNetworkData.GetActiveNetworkInterface(wrongIPAddress, map);
            var unicastIPAddressEnumerable = (IList<UnicastIPAddressInformation>)activeInterface.UnicastIPAddresses;

            Assert.That(activeInterface, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(activeInterface.UnicastIPAddresses, Has.Count.EqualTo(1));
                Assert.That(unicastIPAddressEnumerable[0].Address.ToString(), Is.EqualTo(correctIPAddress.ToString()));
            });
        }

        [Test]
        public void GetNetworkInterfaceData_NotNull()
        {
            // ACT
            var networkInterfaces = _realNetworkData.GetNetworkInterfaceData();

            Assert.That(networkInterfaces, Is.Not.Null);
        }
    }
}
