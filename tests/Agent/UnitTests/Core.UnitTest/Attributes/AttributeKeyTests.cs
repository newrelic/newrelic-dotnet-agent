// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Attributes.Tests;

[TestFixture]
public class AttributeKeyTests
{
    private static readonly Guid _guid = Guid.NewGuid();

    [Test]
    public void SameGuid_NullNames_AreEqual_AndHashEqual()
    {
        var a = new AttributeKey(_guid, null);
        var b = new AttributeKey(_guid, null);

        Assert.Multiple(() =>
        {
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void SameGuid_SameName_AreEqual_AndHashEqual()
    {
        var a = new AttributeKey(_guid, "foo");
        var b = new AttributeKey(_guid, new string("foo".ToCharArray()));

        Assert.Multiple(() =>
        {
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void SameGuid_DifferentName_AreNotEqual()
    {
        var a = new AttributeKey(_guid, "foo");
        var b = new AttributeKey(_guid, "bar");

        Assert.Multiple(() =>
        {
            Assert.That(a.Equals(b), Is.False);
            Assert.That(b.Equals(a), Is.False);
        });
    }

    [Test]
    public void SameGuid_NamesDifferOnlyInCase_AreNotEqual()
    {
        var a = new AttributeKey(_guid, "foo");
        var b = new AttributeKey(_guid, "Foo");

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void DifferentGuid_SameName_AreNotEqual()
    {
        var a = new AttributeKey(Guid.NewGuid(), "foo");
        var b = new AttributeKey(Guid.NewGuid(), "foo");

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void DifferentGuid_NullNames_AreNotEqual()
    {
        var a = new AttributeKey(Guid.NewGuid(), null);
        var b = new AttributeKey(Guid.NewGuid(), null);

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void NullName_vs_NonNullName_AreNotEqual()
    {
        var a = new AttributeKey(_guid, null);
        var b = new AttributeKey(_guid, "foo");

        Assert.Multiple(() =>
        {
            Assert.That(a.Equals(b), Is.False);
            Assert.That(b.Equals(a), Is.False);
        });
    }

    [Test]
    public void StaticKey_HashEqualsGuidHash()
    {
        var key = new AttributeKey(_guid, null);

        Assert.That(key.GetHashCode(), Is.EqualTo(_guid.GetHashCode()));
    }

    [Test]
    public void Properties_ReturnConstructorValues()
    {
        var key = new AttributeKey(_guid, "foo");

        Assert.Multiple(() =>
        {
            Assert.That(key.DefinitionGuid, Is.EqualTo(_guid));
            Assert.That(key.Name, Is.EqualTo("foo"));
        });
    }

    [Test]
    public void EqualsObject_WithNonKey_IsFalse()
    {
        var key = new AttributeKey(_guid, "foo");

        Assert.Multiple(() =>
        {
            Assert.That(key.Equals((object)"foo"), Is.False);
            Assert.That(key.Equals((object)null), Is.False);
        });
    }

    [Test]
    public void EqualsObject_WithBoxedEqualKey_IsTrue()
    {
        var key = new AttributeKey(_guid, "foo");
        object boxed = new AttributeKey(_guid, "foo");

        Assert.That(key.Equals(boxed), Is.True);
    }

    [Test]
    public void EqualsObject_WithBoxedDifferentKey_IsFalse()
    {
        var key = new AttributeKey(_guid, "foo");
        object boxed = new AttributeKey(_guid, "bar");

        Assert.That(key.Equals(boxed), Is.False);
    }
}
