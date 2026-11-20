// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core.Attributes.Tests;

[TestFixture]
public class ResolvedAttributeNameTests
{
    private const AttributeDestinations Destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent;

    private static ResolvedAttributeName Create(string name) => new ResolvedAttributeName(name, AttributeClassification.UserAttributes, Destinations);

    [Test]
    public void Properties_ReturnConstructorValues()
    {
        var resolved = Create("foo");

        Assert.Multiple(() =>
        {
            Assert.That(resolved.Name, Is.EqualTo("foo"));
            Assert.That(resolved.Destinations, Is.EqualTo(Destinations));
        });
    }

    [TestCase("", false)]
    [TestCase(" ", false)]
    [TestCase("a", true)]
    public void IsNameValid_TableOfNames_ShortNames(string name, bool expected)
    {
        Assert.That(Create(name).IsNameValid, Is.EqualTo(expected));
    }

    [TestCase(255, 'a', true)]
    [TestCase(256, 'a', false)]
    [TestCase(85, '€', true)]
    [TestCase(86, '€', false)]
    public void IsNameValid_TableOfNames_ByteLength(int count, char character, bool expected)
    {
        Assert.That(Create(new string(character, count)).IsNameValid, Is.EqualTo(expected));
    }

    [TestCase("foo", true, 0)]
    [TestCase("", false, 1)]
    [TestCase(" ", false, 1)]
    public void IsNameValid_RepeatedReads_ReturnSameResult_AndLogAtMostOnce(string name, bool expected, int expectedLogCount)
    {
        using var logging = new TestUtilities.Logging();
        var resolved = Create(name);
        var countAtConstruction = logging.DebugCount;

        var first = resolved.IsNameValid;
        var second = resolved.IsNameValid;
        var third = resolved.IsNameValid;

        Assert.Multiple(() =>
        {
            Assert.That(countAtConstruction, Is.EqualTo(0));
            Assert.That(first, Is.EqualTo(expected));
            Assert.That(second, Is.EqualTo(expected));
            Assert.That(third, Is.EqualTo(expected));
            Assert.That(logging.DebugCount, Is.EqualTo(expectedLogCount));
        });
    }

    [Test]
    public void IsNameValid_EmptyName_LogsNullOrEmpty()
    {
        using var logging = new TestUtilities.Logging();

        Assert.That(Create("").IsNameValid, Is.False);

        Assert.That(logging.HasMessageThatContains("Name is null/empty"), Is.True);
    }

    [Test]
    public void IsAvailableForAny_TransactionEventOnly_ReturnsTrue()
    {
        Assert.That(Create("foo").IsAvailableForAny(new[] { AttributeDestinations.TransactionEvent }), Is.True);
    }

    [Test]
    public void IsAvailableForAny_SecondTargetMatches_ReturnsTrue()
    {
        Assert.That(Create("foo").IsAvailableForAny(new[] { AttributeDestinations.ErrorEvent, AttributeDestinations.SpanEvent }), Is.True);
    }

    [Test]
    public void IsAvailableForAny_NoTargetMatches_ReturnsFalse()
    {
        Assert.That(Create("foo").IsAvailableForAny(new[] { AttributeDestinations.ErrorEvent }), Is.False);
    }

    [Test]
    public void IsAvailableForAny_EmptyArray_ReturnsFalse()
    {
        Assert.That(Create("foo").IsAvailableForAny(new AttributeDestinations[0]), Is.False);
    }

    [Test]
    public void IsAvailableForAny_Null_ReturnsFalse()
    {
        Assert.That(Create("foo").IsAvailableForAny(null), Is.False);
    }
}
