// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class FunctionIdHasherTests
{
    // Pinned literal for ("Step0App", "Step0App.Worker", "DoWork", "System.Int32"), computed once (FNV-1a 64 over
    // UTF-8 "Step0App|Step0App.Worker|DoWork|System.Int32", then OR 0x8000000000000000).
    private const ulong Step0ExpectedHash = 0xD43654B7FA81DBC4UL;

    [Test]
    public void Compute_IsStableForSameInput()
    {
        var first = FunctionIdHasher.Compute("Step0App", "Step0App.Worker", "DoWork", "System.Int32");
        var second = FunctionIdHasher.Compute("Step0App", "Step0App.Worker", "DoWork", "System.Int32");

        Assert.That(first, Is.EqualTo(Step0ExpectedHash));
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void Compute_DiffersWhenAnyPartDiffers()
    {
        var baseline = FunctionIdHasher.Compute("Assembly", "Type", "Method", "System.Int32");

        Assert.Multiple(() =>
        {
            Assert.That(FunctionIdHasher.Compute("Other", "Type", "Method", "System.Int32"), Is.Not.EqualTo(baseline));
            Assert.That(FunctionIdHasher.Compute("Assembly", "Other", "Method", "System.Int32"), Is.Not.EqualTo(baseline));
            Assert.That(FunctionIdHasher.Compute("Assembly", "Type", "Other", "System.Int32"), Is.Not.EqualTo(baseline));
            Assert.That(FunctionIdHasher.Compute("Assembly", "Type", "Method", "System.String"), Is.Not.EqualTo(baseline));
        });
    }

    [Test]
    public void Hasher_ArityTokens_Distinct()
    {
        var hashes = new HashSet<ulong>();
        for (var arity = 0; arity <= 8; arity++)
        {
            hashes.Add(FunctionIdHasher.Compute("A", "B", "M", "*" + arity));
        }

        hashes.Add(FunctionIdHasher.Compute("A", "B", "M", "System.Int32"));

        Assert.That(hashes, Has.Count.EqualTo(10));
    }

    [Test]
    public void Compute_EmptyRangeKey_IsThePreRangeValue()
    {
        // Unranged entries must keep the class names and function ids they had before version ranges existed.
        Assert.Multiple(() =>
        {
            Assert.That(FunctionIdHasher.Compute("Step0App", "Step0App.Worker", "DoWork", "System.Int32", ""), Is.EqualTo(Step0ExpectedHash));
            Assert.That(FunctionIdHasher.Compute("A", "B", "M", "*3", ""), Is.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "*3")));
        });
    }

    [Test]
    public void Compute_NonEmptyRangeKey_HashesTheRangeAfterAPipe()
    {
        var ranged = FunctionIdHasher.Compute("A", "B", "M", "System.Int32", "1.0.0-5.65535.65535");

        Assert.Multiple(() =>
        {
            Assert.That(ranged, Is.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "System.Int32|1.0.0-5.65535.65535")));
            Assert.That(ranged, Is.Not.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "System.Int32")));
            Assert.That(ranged, Is.Not.EqualTo(FunctionIdHasher.Compute("A", "B", "M", "System.Int32", "6.0.0-65535.65535.65535")));
        });
    }

    [Test]
    public void Compute_HighBitSet()
    {
        var hash = FunctionIdHasher.Compute("Assembly", "Type", "Method", "");

        Assert.That(hash & 0x8000000000000000UL, Is.EqualTo(0x8000000000000000UL));
    }
}
