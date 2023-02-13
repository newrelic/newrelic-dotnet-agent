// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using System.Runtime.Remoting.Messaging;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utils
{
    [TestFixture]
    public class SecurityProtocolTypeExtensionsTest
    {
        [TestCase(0x0, ExpectedResult = "Using System Default Settings")] // because .SystemDefault enum value doesn't exist for some platforms
        [TestCase(0x000C, ExpectedResult = "SSL 2")] // because .Ssl2 enum value doesn't exist for some platforms
        [TestCase(SecurityProtocolType.Ssl3, ExpectedResult = "SSL 3")]
        [TestCase(SecurityProtocolType.Tls, ExpectedResult = "TLS 1.0")]
        [TestCase(SecurityProtocolType.Tls11, ExpectedResult = "TLS 1.1")]
        [TestCase(SecurityProtocolType.Tls12, ExpectedResult = "TLS 1.2")]
        [TestCase(SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls, ExpectedResult = "SSL 3, TLS 1.0")]
        [TestCase(0x3000, ExpectedResult = "TLS 1.3")] // because .Tls13 enum value doesn't exist for some platforms
        public string ToFriendlyString_Test(SecurityProtocolType securityProtocolType)
        {
            return securityProtocolType.ToFriendlyString();
        }
    }
}
