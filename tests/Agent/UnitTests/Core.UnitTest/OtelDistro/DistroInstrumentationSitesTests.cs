// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using static NewRelic.Agent.Core.OtelDistro.InstrumentationDefinitionsTableTestData;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroInstrumentationSitesTests
{
    [Test]
    public void TryGet_SameIndexTwice_ReturnsSameInstance()
    {
        var sites = ReadGoldenSites();

        var first = sites.TryGet(1, out var firstSite);
        var second = sites.TryGet(1, out var secondSite);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(firstSite, Is.Not.Null);
            Assert.That(secondSite, Is.SameAs(firstSite));
        });
    }

    [TestCase(-1)]
    [TestCase(3)] // Count
    [TestCase(int.MaxValue)]
    public void TryGet_NegativeOrPastEnd_ReturnsFalseAndNull(int index)
    {
        var sites = ReadGoldenSites();

        var found = sites.TryGet(index, out var site);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(site, Is.Null);
        });
    }

    [Test]
    public void TryGet_ConcurrentFirstCalls_ReturnSameInstance()
    {
        const int threadCount = 8;
        var sites = ReadGoldenSites();
        var results = new DistroInstrumentationSite[threadCount];
        var found = new bool[threadCount];
        using var ready = new CountdownEvent(threadCount);
        using var go = new ManualResetEventSlim(false);

        var threads = Enumerable.Range(0, threadCount)
            .Select(i => new Thread(() =>
            {
                ready.Signal();
                go.Wait();
                found[i] = sites.TryGet(2, out results[i]);
            }))
            .ToList();
        threads.ForEach(t => t.Start());
        ready.Wait();
        go.Set();
        threads.ForEach(t => t.Join());

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.All.True);
            Assert.That(results, Is.All.Not.Null);
            Assert.That(results.Distinct().Count(), Is.EqualTo(1), "every thread must get the one published instance");
            Assert.That(sites.TryGet(2, out var later), Is.True);
            Assert.That(later, Is.SameAs(results[0]));
        });
    }

    [Test]
    public void Sites_DefaultOffRecord_StartsOff()
    {
        var table = Build(
            new Row { Method = "On" },
            new Row { Method = "Off", DefaultOff = true },
            new Row { Method = "AlsoOn" });

        var sites = Read(table);

        Assert.Multiple(() =>
        {
            Assert.That(sites.IsDefaultOff(1), Is.True);
            Assert.That(sites.IsOn(0), Is.True);
            Assert.That(sites.IsOn(1), Is.False);
            Assert.That(sites.IsOn(2), Is.True);
        });
    }

    [Test]
    public void Sites_LogForwardingFlag_IsReadPerRecord()
    {
        var table = Build(
            new Row { Method = "Plain" },
            new Row { Method = "LogSite", LogForwarding = true },
            new Row { Method = "OffLogSite", DefaultOff = true, LogForwarding = true });

        var sites = Read(table);

        Assert.Multiple(() =>
        {
            Assert.That(sites.IsLogForwarding(0), Is.False);
            Assert.That(sites.IsLogForwarding(1), Is.True);
            // Independent of the gate: a default-off log-forwarding site still reads as log-forwarding.
            Assert.That(sites.IsLogForwarding(2), Is.True);
            Assert.That(sites.TryGet(1, out var site), Is.True);
            Assert.That(site.LogForwarding, Is.True);
        });
    }

    [TestCase(-1)]
    [TestCase(3)]
    [TestCase(int.MaxValue)]
    public void Sites_IsLogForwardingOutOfRange_False(int index)
    {
        var sites = Read(Build(new Row { LogForwarding = true }));

        Assert.That(sites.IsLogForwarding(index), Is.False);
    }

    [Test]
    public void Sites_SetOn_SwapsState()
    {
        var sites = ReadGoldenSites();

        sites.SetOn(new[] { false, true, true });

        Assert.Multiple(() =>
        {
            Assert.That(sites.IsOn(0), Is.False);
            Assert.That(sites.IsOn(1), Is.True);
            Assert.That(sites.IsOn(2), Is.True, "a default-off site can be turned on");
            Assert.That(sites.IsDefaultOff(2), Is.True, "the default does not change");
        });
    }

    [Test]
    public void Sites_SetOn_CopiesTheArray()
    {
        var sites = ReadGoldenSites();
        var on = new[] { true, true, true };

        sites.SetOn(on);
        on[0] = false;

        Assert.That(sites.IsOn(0), Is.True, "a later change to the caller's array must not reach the gate");
    }

    [TestCase(2)]
    [TestCase(4)]
    [TestCase(0)]
    public void Sites_SetOnWrongLength_Throws(int length)
    {
        var sites = ReadGoldenSites();

        var ex = Assert.Throws<ArgumentException>(() => sites.SetOn(new bool[length]));

        Assert.Multiple(() =>
        {
            Assert.That(ex.ParamName, Is.EqualTo("on"));
            Assert.That(sites.IsOn(0), Is.True, "state unchanged");
            Assert.That(sites.IsOn(2), Is.False, "state unchanged");
        });
    }

    [Test]
    public void Sites_SetOnNull_Throws()
    {
        var sites = ReadGoldenSites();

        Assert.Throws<ArgumentNullException>(() => sites.SetOn(null));
    }

    [TestCase(-1)]
    [TestCase(3)] // Count
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void Sites_IsOnOutOfRange_False(int index)
    {
        var sites = ReadGoldenSites();

        Assert.That(sites.IsOn(index), Is.False);
    }

    [TestCase(-1)]
    [TestCase(3)] // Count
    public void Sites_PerIndexReadersOutOfRange_Throw(int index)
    {
        var sites = ReadGoldenSites();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => sites.PointKey(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => sites.FunctionId(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => sites.SourceFile(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => sites.IsDefaultOff(index));
        });
    }

    [Test]
    public void Sites_FunctionId_MatchesEachSitesFunctionId()
    {
        var sites = ReadGoldenSites();
        var ids = new ulong[sites.Count];
        for (var i = 0; i < sites.Count; i++)
        {
            ids[i] = sites.FunctionId(i);
        }

        var expected = new ulong[sites.Count];
        for (var i = 0; i < sites.Count; i++)
        {
            sites.TryGet(i, out var site);
            expected[i] = site.FunctionId;
        }

        Assert.That(ids, Is.EqualTo(expected));
    }

    [Test]
    public void Sites_EmptyTable_SetOnEmptyArray_IsAccepted()
    {
        var sites = Read(Build());

        sites.SetOn(new bool[0]);

        Assert.That(sites.IsOn(0), Is.False);
    }

    private static DistroInstrumentationSites Read(byte[] table)
    {
        var ok = InstrumentationDefinitionsTable.TryRead(table, out _, out var sites, out _, out var failureReason);
        Assert.That(ok, Is.True, failureReason);
        return sites;
    }

    private static DistroInstrumentationSites ReadGoldenSites()
    {
        var ok = InstrumentationDefinitionsTable.TryRead(GoldenTwoAssemblies, out _, out var sites, out _, out var failureReason);
        Assert.That(ok, Is.True, failureReason);
        return sites;
    }
}
