// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using System.Threading;
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

    [Test]
    public void GetSysGettidNumber_X64_Returns186()
    {
        var result = CurrentOsThreadIdProvider.GetSysGettidNumber(Architecture.X64);

        Assert.That(result, Is.EqualTo(186));
    }

    [Test]
    public void GetSysGettidNumber_Arm_Returns224()
    {
        var result = CurrentOsThreadIdProvider.GetSysGettidNumber(Architecture.Arm);

        Assert.That(result, Is.EqualTo(224));
    }

    [Test]
    public void GetSysGettidNumber_Arm64_Returns178()
    {
        var result = CurrentOsThreadIdProvider.GetSysGettidNumber(Architecture.Arm64);

        Assert.That(result, Is.EqualTo(178));
    }

    [Test]
    public void GetSysGettidNumber_UnsupportedArchitecture_ThrowsPlatformNotSupportedException()
    {
        Assert.That(() => CurrentOsThreadIdProvider.GetSysGettidNumber(Architecture.X86),
            Throws.TypeOf<PlatformNotSupportedException>());
    }

    // Exercises the real syscall(2) path (line 44 of ICurrentOsThreadIdProvider.cs), which only
    // runs on Linux. No CI job currently runs managed unit tests on Linux (unit_tests.yml's only
    // test job is windows-latest), so this test only runs when the suite is run on Linux locally.
    [Test]
    [Platform("Linux")]
    public void GetCurrentOsThreadId_OnLinux_ReturnsPositiveIdThatDiffersAcrossThreads()
    {
        var provider = new CurrentOsThreadIdProvider();

        var mainThreadId = provider.GetCurrentOsThreadId();

        long otherThreadId = 0;
        var thread = new Thread(() => otherThreadId = provider.GetCurrentOsThreadId());
        thread.Start();
        thread.Join();

        Assert.Multiple(() =>
        {
            Assert.That(mainThreadId, Is.GreaterThan(0));
            Assert.That(otherThreadId, Is.GreaterThan(0));
            Assert.That(otherThreadId, Is.Not.EqualTo(mainThreadId));
        });
    }
}
