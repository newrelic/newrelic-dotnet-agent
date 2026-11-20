// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Attributes.Tests;

[TestFixture]
public class AttributeValueTests
{
    public enum ValueKind
    {
        Core,
        InfiniteTracing
    }

    private static AttributeDefinition CreateStaticDefinition()
    {
        return new AttributeDefinition("test.static", AttributeClassification.AgentAttributes, new Dictionary<AttributeDestinations, bool>
        {
            { AttributeDestinations.TransactionEvent, true }
        });
    }

    private static AttributeDefinition CreateFamilyDefinition()
    {
        return new AttributeDefinition("test.family.*", AttributeClassification.UserAttributes, new Dictionary<AttributeDestinations, bool>());
    }

    private static IAttributeValue CreateStaticValue(ValueKind type, AttributeDefinition definition)
    {
        return type == ValueKind.Core ? new AttributeValue(definition) : new Segments.AttributeValue(definition);
    }

    private static IAttributeValue CreateFamilyValue(ValueKind type, AttributeDefinition definition, string name, AttributeDestinations destinations)
    {
        return type == ValueKind.Core ? new AttributeValue(definition, name, destinations) : new Segments.AttributeValue(definition, name, destinations);
    }

    [TestCase(ValueKind.Core)]
    [TestCase(ValueKind.InfiniteTracing)]
    public void StaticValue_NameAndDestinations_FromDefinition(ValueKind type)
    {
        var definition = CreateStaticDefinition();

        var value = CreateStaticValue(type, definition);

        Assert.Multiple(() =>
        {
            Assert.That(value.Name, Is.EqualTo("test.static"));
            Assert.That(value.AttributeDestinations, Is.EqualTo(AttributeDestinations.TransactionEvent));
        });
    }

    [TestCase(ValueKind.Core)]
    [TestCase(ValueKind.InfiniteTracing)]
    public void StaticValue_KeyNameIsNull(ValueKind type)
    {
        var definition = CreateStaticDefinition();

        var value = CreateStaticValue(type, definition);

        Assert.Multiple(() =>
        {
            Assert.That(value.Key.Name, Is.Null);
            Assert.That(value.Key.DefinitionGuid, Is.EqualTo(definition.Guid));
            Assert.That(value.Key.Equals(new AttributeKey(definition.Guid, null)), Is.True);
        });
    }

    [TestCase(ValueKind.Core)]
    [TestCase(ValueKind.InfiniteTracing)]
    public void FamilyValue_NameAndDestinations_FromValue(ValueKind type)
    {
        var definition = CreateFamilyDefinition();

        var value = CreateFamilyValue(type, definition, "foo", AttributeDestinations.SpanEvent);

        Assert.Multiple(() =>
        {
            Assert.That(value.Name, Is.EqualTo("foo"));
            Assert.That(value.AttributeDestinations, Is.EqualTo(AttributeDestinations.SpanEvent));
            Assert.That(value.Key.Equals(new AttributeKey(definition.Guid, "foo")), Is.True);
        });
    }

    [TestCase(ValueKind.Core)]
    [TestCase(ValueKind.InfiniteTracing)]
    public void FamilyValue_WithDestinationsNone_KeepsNone(ValueKind type)
    {
        var definition = CreateFamilyDefinition();

        var value = CreateFamilyValue(type, definition, "foo", AttributeDestinations.None);

        Assert.That(value.AttributeDestinations, Is.EqualTo(AttributeDestinations.None));
    }

    [TestCase(ValueKind.Core)]
    [TestCase(ValueKind.InfiniteTracing)]
    public void SegmentsCopy_OfStaticValue_KeepsNullKeyName(ValueKind source)
    {
        var definition = CreateStaticDefinition();
        var original = CreateStaticValue(source, definition);
        original.Value = "bar";

        var copy = new Segments.AttributeValue(original);

        Assert.Multiple(() =>
        {
            Assert.That(copy.Key.Name, Is.Null);
            Assert.That(copy.Key.Equals(original.Key), Is.True);
            Assert.That(copy.Name, Is.EqualTo("test.static"));
            Assert.That(copy.AttributeDestinations, Is.EqualTo(AttributeDestinations.TransactionEvent));
            Assert.That(copy.Value, Is.EqualTo("bar"));
        });
    }

    [TestCase(ValueKind.Core)]
    [TestCase(ValueKind.InfiniteTracing)]
    public void SegmentsCopy_OfFamilyValue_KeepsNameAndDestinations(ValueKind source)
    {
        var definition = CreateFamilyDefinition();
        var original = CreateFamilyValue(source, definition, "foo", AttributeDestinations.SpanEvent);
        original.Value = "bar";

        var copy = new Segments.AttributeValue(original);

        Assert.Multiple(() =>
        {
            Assert.That(copy.Name, Is.EqualTo("foo"));
            Assert.That(copy.Key.Equals(new AttributeKey(definition.Guid, "foo")), Is.True);
            Assert.That(copy.AttributeDestinations, Is.EqualTo(AttributeDestinations.SpanEvent));
            Assert.That(copy.Value, Is.EqualTo("bar"));
        });
    }

    [Test]
    public void SegmentsCopy_OfFamilyValueWithLazyValue_KeepsNameAndLazyValue()
    {
        var definition = CreateFamilyDefinition();
        var original = new AttributeValue(definition, "foo", AttributeDestinations.SpanEvent)
        {
            LazyValue = new Lazy<object>(() => "lazy")
        };

        var copy = new Segments.AttributeValue(original);
        copy.MakeImmutable();

        Assert.Multiple(() =>
        {
            Assert.That(copy.Name, Is.EqualTo("foo"));
            Assert.That(copy.Value, Is.EqualTo("lazy"));
        });
    }
}
