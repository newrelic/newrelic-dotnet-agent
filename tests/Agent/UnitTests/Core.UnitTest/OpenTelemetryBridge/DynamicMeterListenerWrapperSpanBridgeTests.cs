// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge;

[TestFixture]
public class DynamicMeterListenerWrapperSpanBridgeTests
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

    // Dedicated type so its delegate cache initializes, and logs, inside the log-once test.
    private readonly struct FakeSpanWithoutToArrayForLogging
    {
        public int Length => 0;
    }

    private readonly struct FakeSpanWithToArrayForLogging
    {
        public KeyValuePair<string, object>[] ToArray() => Array.Empty<KeyValuePair<string, object>>();
    }

    // A static ToArray makes Expression.Call throw, which exercises the delegate creation failure path.
    private readonly struct FakeSpanWithStaticToArray
    {
        public static KeyValuePair<string, object>[] ToArray() => Array.Empty<KeyValuePair<string, object>>();
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
        var span = new FakeSpanWithTags(Array.Empty<KeyValuePair<string, object>>());

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

    [Test]
    public void ToTagArray_WhenSpanTypeHasNoToArrayMethod_LogsWarningOnlyOnce()
    {
        using (var logging = new TestUtilities.Logging())
        {
            DynamicMeterListenerWrapper.ToTagArray(new FakeSpanWithoutToArrayForLogging());
            DynamicMeterListenerWrapper.ToTagArray(new FakeSpanWithoutToArrayForLogging());

            Assert.Multiple(() =>
            {
                Assert.That(logging.WarnCount, Is.EqualTo(1));
                Assert.That(logging.HasMessageThatContains("no ToArray method"), Is.True);
                Assert.That(logging.HasMessageThatContains(nameof(FakeSpanWithoutToArrayForLogging)), Is.True);
            });
        }
    }

    [Test]
    public void ToTagArray_WhenSpanTypeHasToArrayMethod_DoesNotLogWarning()
    {
        using (var logging = new TestUtilities.Logging())
        {
            DynamicMeterListenerWrapper.ToTagArray(new FakeSpanWithToArrayForLogging());

            Assert.That(logging.WarnCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void ToTagArray_WhenDelegateCannotBeCreated_ReturnsEmptyArrayAndLogsWarningOnlyOnce()
    {
        using (var logging = new TestUtilities.Logging())
        {
            var first = DynamicMeterListenerWrapper.ToTagArray(new FakeSpanWithStaticToArray());
            var second = DynamicMeterListenerWrapper.ToTagArray(new FakeSpanWithStaticToArray());

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.Not.Null.And.Empty);
                Assert.That(second, Is.Not.Null.And.Empty);
                Assert.That(logging.WarnCount, Is.EqualTo(1));
                Assert.That(logging.HasMessageThatContains("could not be created"), Is.True);
            });
        }
    }

    // On .NET Framework the old get_Item reflection path threw here, so the callback never ran.
    [Test]
    public void HandleMeasurementCallbackWithSpan_ForwardsMeasurementWithTags()
    {
        var instrument = new object();
        var state = new object();
        var span = new FakeSpanWithTags(new[]
        {
            new KeyValuePair<string, object>("route", "/api/test"),
            new KeyValuePair<string, object>("status", 200)
        });

        var callCount = 0;
        object receivedInstrument = null;
        object receivedState = null;
        var receivedMeasurement = 0;
        KeyValuePair<string, object>[] receivedTags = null;

        using (var logging = new TestUtilities.Logging())
        {
            DynamicMeterListenerWrapper.HandleMeasurementCallbackWithSpan(instrument, 42, span, state,
                (inst, measurement, tags, st) =>
                {
                    callCount++;
                    receivedInstrument = inst;
                    receivedMeasurement = measurement;
                    receivedTags = tags.ToArray();
                    receivedState = st;
                });

            Assert.Multiple(() =>
            {
                Assert.That(logging.ErrorCount, Is.EqualTo(0));
                Assert.That(callCount, Is.EqualTo(1));
                Assert.That(receivedInstrument, Is.SameAs(instrument));
                Assert.That(receivedMeasurement, Is.EqualTo(42));
                Assert.That(receivedState, Is.SameAs(state));
                Assert.That(receivedTags, Is.EqualTo(new[]
                {
                    new KeyValuePair<string, object>("route", "/api/test"),
                    new KeyValuePair<string, object>("status", 200)
                }));
            });
        }
    }

    [Test]
    public void HandleMeasurementCallbackWithSpan_WhenCallbackThrows_LogsErrorAndDoesNotThrow()
    {
        var span = new FakeSpanWithTags(Array.Empty<KeyValuePair<string, object>>());

        using (var logging = new TestUtilities.Logging())
        {
            Assert.DoesNotThrow(() => DynamicMeterListenerWrapper.HandleMeasurementCallbackWithSpan(new object(), 1, span, null,
                (inst, measurement, tags, st) => throw new InvalidOperationException("boom")));

            Assert.Multiple(() =>
            {
                Assert.That(logging.ErrorCount, Is.EqualTo(1));
                Assert.That(logging.HasErrorMessageThatContains("boom"), Is.True);
            });
        }
    }
}
