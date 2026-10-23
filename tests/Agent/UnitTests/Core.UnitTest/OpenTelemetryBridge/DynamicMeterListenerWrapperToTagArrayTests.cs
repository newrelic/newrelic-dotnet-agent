// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge;

[TestFixture]
public class DynamicMeterListenerWrapperToTagArrayTests
{
    // Mimics a ReadOnlySpan from another assembly: a by-ref returning indexer (which reflection cannot call on
    // .NET Framework) plus a ToArray method.
    private readonly struct FakeSpanWithTags
    {
        private readonly KeyValuePair<string, object>[] _items;

        public FakeSpanWithTags(KeyValuePair<string, object>[] items) => _items = items;

        public int Length => _items.Length;

        public ref readonly KeyValuePair<string, object> this[int index] => ref _items[index];

        public KeyValuePair<string, object>[] ToArray() => _items;
    }

    private readonly struct FakeSpanReturningNull
    {
        public KeyValuePair<string, object>[] ToArray() => null;
    }

    private readonly struct FakeSpanWithoutToArray
    {
        public int Length => 0;
    }

    [Test]
    public void ToTagArray_CopiesTagsWithKeysAndValues()
    {
        var span = new FakeSpanWithTags(new[]
        {
            new KeyValuePair<string, object>("method", "GET"),
            new KeyValuePair<string, object>("status", 200),
            new KeyValuePair<string, object>("nullable", null)
        });

        var result = DynamicMeterListenerWrapper.ToTagArray(span);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Length.EqualTo(3));
            Assert.That(result[0].Key, Is.EqualTo("method"));
            Assert.That(result[0].Value, Is.EqualTo("GET"));
            Assert.That(result[1].Key, Is.EqualTo("status"));
            Assert.That(result[1].Value, Is.EqualTo(200));
            Assert.That(result[2].Key, Is.EqualTo("nullable"));
            Assert.That(result[2].Value, Is.Null);
        });
    }

    [Test]
    public void ToTagArray_WhenToArrayReturnsEmptyArray_ReturnsEmptyArray()
    {
        var span = new FakeSpanWithTags(new KeyValuePair<string, object>[0]);

        var result = DynamicMeterListenerWrapper.ToTagArray(span);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ToTagArray_WhenToArrayReturnsNull_ReturnsEmptyArray()
    {
        var result = DynamicMeterListenerWrapper.ToTagArray(new FakeSpanReturningNull());

        Assert.That(result, Is.Not.Null.And.Empty);
    }

    [Test]
    public void ToTagArray_WhenSpanTypeHasNoToArrayMethod_ReturnsEmptyArray()
    {
        var result = DynamicMeterListenerWrapper.ToTagArray(new FakeSpanWithoutToArray());

        Assert.That(result, Is.Not.Null.And.Empty);
    }
}
