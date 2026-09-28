// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.Utilities;

[TestFixture]
public class CurrentOsThreadIdProviderTests
{
    [Test]
    public void GetCurrentOsThreadId_ReturnsNonZero()
    {
        var provider = new CurrentOsThreadIdProvider();

        var result = provider.GetCurrentOsThreadId();

        Assert.That(result, Is.Not.EqualTo(0));
    }

    [Test]
    public void GetCurrentOsThreadId_IsStableAcrossCallsOnSameThread()
    {
        var provider = new CurrentOsThreadIdProvider();

        var first = provider.GetCurrentOsThreadId();
        var second = provider.GetCurrentOsThreadId();

        Assert.That(second, Is.EqualTo(first));
    }
}
