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
    public void EqualsObject_BoxedEqualKey_IsTrue_AndHashesMatch_AndNonKeyIsFalse()
    {
        var staticKey = new AttributeKey(_guid, null);
        var familyKey = new AttributeKey(_guid, "foo");
        object boxedStatic = new AttributeKey(_guid, null);
        object boxedFamily = new AttributeKey(_guid, new string("foo".ToCharArray()));

        Assert.Multiple(() =>
        {
            Assert.That(staticKey.Equals(boxedStatic), Is.True);
            Assert.That(familyKey.Equals(boxedFamily), Is.True);
            Assert.That(boxedStatic.GetHashCode(), Is.EqualTo(staticKey.GetHashCode()));
            Assert.That(boxedFamily.GetHashCode(), Is.EqualTo(familyKey.GetHashCode()));
            Assert.That(familyKey.Equals((object)"foo"), Is.False);
        });
    }
}
