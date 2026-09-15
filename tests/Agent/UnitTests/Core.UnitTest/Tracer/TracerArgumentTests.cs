// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core.Tracer;

[TestFixture]
public class TracerArgumentTests
{
    private const uint AsyncBit = 1 << 23;
    private const uint RuntimeAsyncBit = 1 << 19;

    [Test]
    public void IsRuntimeAsync_IsTrue_WhenBit19IsSet()
    {
        Assert.That(TracerArgument.IsRuntimeAsync(RuntimeAsyncBit), Is.True);
    }

    [Test]
    public void IsRuntimeAsync_IsFalse_WhenNoFlagsAreSet()
    {
        Assert.That(TracerArgument.IsRuntimeAsync(0), Is.False);
    }

    [Test]
    public void IsRuntimeAsync_IsFalse_WhenOnlyTheAsyncBitIsSet()
    {
        Assert.That(TracerArgument.IsRuntimeAsync(AsyncBit), Is.False);
    }

    [Test]
    public void IsAsync_IsFalse_WhenOnlyTheRuntimeAsyncBitIsSet()
    {
        Assert.That(TracerArgument.IsAsync(RuntimeAsyncBit), Is.False);
    }

    [Test]
    public void BothFlags_AreIndependent_WhenBothBitsAreSet()
    {
        var tracerArguments = AsyncBit | RuntimeAsyncBit;

        Assert.Multiple(() =>
        {
            Assert.That(TracerArgument.IsAsync(tracerArguments), Is.True);
            Assert.That(TracerArgument.IsRuntimeAsync(tracerArguments), Is.True);
        });
    }

    // Guards the C++/C# bit-layout contract. Configuration/TracerFlags.h declares
    // RuntimeAsyncMethod = 1 << 19; nothing else can verify that across the language
    // boundary, so pin the literal here.
    [Test]
    public void RuntimeAsyncFlag_IsBit19()
    {
        Assert.That((uint)TracerFlags.RuntimeAsync, Is.EqualTo(524288u));
    }

    [Test]
    public void IsRuntimeAsync_IsFalse_WhenANeighbouringBitIsSet()
    {
        // Bit 20 is AttributeInstrumentation, bit 18 is the top of the 3-bit
        // instrumentation level. Neither may be mistaken for runtime-async.
        Assert.Multiple(() =>
        {
            Assert.That(TracerArgument.IsRuntimeAsync(1 << 20), Is.False);
            Assert.That(TracerArgument.IsRuntimeAsync(1 << 18), Is.False);
        });
    }
}
