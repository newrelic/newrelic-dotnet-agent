// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    [TestFixture]
    public class ActivityStatusCodeExtensionsTests
    {
        [TestCase(0, ExpectedResult = "unset")]
        [TestCase(1, ExpectedResult = "ok")]
        [TestCase(2, ExpectedResult = "error")]
        [TestCase(3, ExpectedResult = "3")]
        [TestCase(-1, ExpectedResult = "-1")]
        public string ToActivityStatusCodeString_Returns_Expected_String(int statusCode)
        {
            return statusCode.ToActivityStatusCodeString();
        }
    }
}
