// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroPayloadIdTests
{
    [Test]
    public void For_IsOrderIndependent()
    {
        Assert.That(DistroPayloadId.For(new ulong[] { 3, 1, 2 }), Is.EqualTo(DistroPayloadId.For(new ulong[] { 1, 2, 3 })));
    }

    [Test]
    public void For_Is32UppercaseHexChars()
    {
        var id = DistroPayloadId.For(new ulong[] { 0x8000000000000001, 42 });

        Assert.That(id, Does.Match("^[0-9A-F]{32}$"));
    }

    [Test]
    public void For_DifferentSetsDiffer()
    {
        var a = DistroPayloadId.For(new ulong[] { 1, 2 });
        var b = DistroPayloadId.For(new ulong[] { 1, 3 });
        var c = DistroPayloadId.For(new ulong[] { 1 });

        Assert.That(new[] { a, b, c }.Distinct().Count(), Is.EqualTo(3));
    }

    [Test]
    public void For_DuplicateIds_CountOnce()
    {
        Assert.That(DistroPayloadId.For(new ulong[] { 5, 9, 5 }), Is.EqualTo(DistroPayloadId.For(new ulong[] { 9, 5 })));
    }

    [Test]
    public void For_RunsOfDuplicatesAnywhere_SameAsTheDistinctSet()
    {
        var expected = DistroPayloadId.For(new ulong[] { 1, 4, 9 });

        Assert.Multiple(() =>
        {
            Assert.That(DistroPayloadId.For(new ulong[] { 9, 9, 9, 1, 1, 4, 4, 9, 1 }), Is.EqualTo(expected));
            Assert.That(DistroPayloadId.For(new ulong[] { 7, 7, 7 }), Is.EqualTo(DistroPayloadId.For(new ulong[] { 7 })));
            Assert.That(DistroPayloadId.For(new System.Collections.Generic.List<ulong> { 4, 1, 9, 4 }), Is.EqualTo(expected), "any enumerable");
        });
    }

    [Test]
    public void For_HalvesUseDifferentSeeds()
    {
        var id = DistroPayloadId.For(new ulong[] { 7 });

        Assert.That(id.Substring(0, 16), Is.Not.EqualTo(id.Substring(16)));
    }

    [Test]
    public void For_KnownValue_IsStable()
    {
        // FNV-1a 64 over UTF-8 "NRIDT-A" then the id's 8 bytes little-endian; same with "NRIDT-B".
        Assert.That(DistroPayloadId.For(new ulong[] { 1 }), Is.EqualTo(Expected(1)));
    }

    [Test]
    public void For_EmptyOrNull_ReturnsNull()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DistroPayloadId.For(new ulong[0]), Is.Null);
            Assert.That(DistroPayloadId.For(null), Is.Null);
        });
    }

    [Test]
    public void ForAppDomain_IsContentIdPlusAppDomainSuffix()
    {
        var ids = new ulong[] { 1, 2, 3 };

        Assert.That(DistroPayloadId.ForAppDomain(ids, 3), Is.EqualTo(DistroPayloadId.For(ids) + "-AD3"));
    }

    [Test]
    public void ForAppDomain_DifferentAppDomainIds_Differ()
    {
        var ids = new ulong[] { 1, 2, 3 };

        Assert.That(DistroPayloadId.ForAppDomain(ids, 1), Is.Not.EqualTo(DistroPayloadId.ForAppDomain(ids, 2)));
    }

    [Test]
    public void ForAppDomain_SameInputs_SameId()
    {
        var first = DistroPayloadId.ForAppDomain(new ulong[] { 1, 2 }, 5);
        var again = DistroPayloadId.ForAppDomain(new ulong[] { 1, 2 }, 5);
        var reordered = DistroPayloadId.ForAppDomain(new ulong[] { 2, 1 }, 5);

        Assert.Multiple(() =>
        {
            Assert.That(again, Is.EqualTo(first));
            Assert.That(reordered, Is.EqualTo(first), "order independent");
        });
    }

    [Test]
    public void ForAppDomain_EmptyOrNull_ReturnsNull()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DistroPayloadId.ForAppDomain(new ulong[0], 1), Is.Null);
            Assert.That(DistroPayloadId.ForAppDomain(null, 1), Is.Null);
        });
    }

    [Test]
    public void ForAppDomain_NegativeOrLargeAppDomainId_InvariantDigits()
    {
        var ids = new ulong[] { 1 };
        var content = DistroPayloadId.For(ids);

        Assert.Multiple(() =>
        {
            Assert.That(DistroPayloadId.ForAppDomain(ids, -1), Is.EqualTo(content + "-AD-1"));
            Assert.That(DistroPayloadId.ForAppDomain(ids, int.MaxValue), Is.EqualTo(content + "-AD2147483647"));
        });
    }

    [Test]
    public void ForAppDomain_NeverEqualsContentOnlyId()
    {
        var ids = new ulong[] { 1, 2 };

        Assert.That(DistroPayloadId.ForAppDomain(ids, 0), Is.Not.EqualTo(DistroPayloadId.For(ids)));
    }

    // An independent reference implementation of the documented algorithm.
    private static string Expected(params ulong[] ids)
    {
        ulong Hash(string seed)
        {
            var hash = 14695981039346656037UL;
            foreach (var b in System.Text.Encoding.UTF8.GetBytes(seed).Concat(ids.OrderBy(i => i).SelectMany(System.BitConverter.GetBytes)))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }

            return hash;
        }

        return Hash("NRIDT-A").ToString("X16") + Hash("NRIDT-B").ToString("X16");
    }
}
