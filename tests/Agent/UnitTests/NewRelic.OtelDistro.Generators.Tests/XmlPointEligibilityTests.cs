// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class XmlPointEligibilityTests
{
    private static XmlInstrumentationPoint Point(string className = "Ns.Class", int? parameterCount = 1, string min = null, string max = null) =>
        new XmlInstrumentationPoint("F", "M", "Asm", className, "Method", parameterCount == null ? null : Enumerable.Repeat("System.Int32", parameterCount.Value).ToArray(), min, max);

    [Test]
    public void Check_OrdinaryPoint_EmittableWithItsRange()
    {
        var result = XmlPointEligibility.Check(Point(min: "1.0", max: "2.0"), out var range);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(XmlPointEligibilityResult.Emittable));
            Assert.That(range.RangeKey, Is.EqualTo(VersionRangeConverter.Convert("1.0", "2.0").RangeKey));
        });
    }

    [TestCase(null)]
    [TestCase(0)]
    [TestCase(XmlPointEligibility.MaxParameters)]
    public void Check_OverloadWideOrUpToMaxParameters_Emittable(int? parameterCount)
    {
        Assert.That(XmlPointEligibility.Check(Point(parameterCount: parameterCount), out var range), Is.EqualTo(XmlPointEligibilityResult.Emittable));
        Assert.That(range, Is.Not.Null);
    }

    [Test]
    public void Check_NullClassName_IsNotABadClassName()
    {
        Assert.That(XmlPointEligibility.Check(Point(className: null), out _), Is.EqualTo(XmlPointEligibilityResult.Emittable));
    }

    [TestCase("")]
    [TestCase(" Ns.Class")]
    [TestCase("Ns.Class ")]
    public void Check_EmptyOrPaddedClassName_BadClassNameNoRange(string className)
    {
        Assert.That(XmlPointEligibility.Check(Point(className: className), out var range), Is.EqualTo(XmlPointEligibilityResult.BadClassName));
        Assert.That(range, Is.Null);
    }

    [Test]
    public void Check_MoreThanMaxParameters_TooManyNoRange()
    {
        Assert.That(XmlPointEligibility.Check(Point(parameterCount: XmlPointEligibility.MaxParameters + 1), out var range), Is.EqualTo(XmlPointEligibilityResult.TooManyParameters));
        Assert.That(range, Is.Null);
    }

    [Test]
    public void Check_EmptyRange_EmptyRangeWithTheRange()
    {
        Assert.That(XmlPointEligibility.Check(Point(min: "2.0", max: "1.0"), out var range), Is.EqualTo(XmlPointEligibilityResult.EmptyRange));
        Assert.That(range.IsEmpty, Is.True);
    }

    [Test]
    public void MaxParameters_IsTheEmittersLimit()
    {
        Assert.That(IntegrationSourceEmitter.MaxParameters, Is.EqualTo(8).And.EqualTo(XmlPointEligibility.MaxParameters));
    }
}
