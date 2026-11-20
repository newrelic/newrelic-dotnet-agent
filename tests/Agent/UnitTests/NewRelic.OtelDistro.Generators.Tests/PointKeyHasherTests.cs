// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class PointKeyHasherTests
{
    private static XmlInstrumentationPoint Point(string[] parameters = null, string factory = "F", string metric = "M") =>
        new XmlInstrumentationPoint(factory, metric, "Asm", "Ns.Class", "Method", parameters);

    [Test]
    public void PointKey_OverloadWideUsesStar()
    {
        var overloadWide = PointKeyHasher.Compute(Point(), "Ns.Class", "");

        // The documented layout spelled out independently of the production string building.
        Assert.That(overloadWide, Is.EqualTo(FnvOf("Asm|Ns.Class|Method|*||F|M")));
    }

    [Test]
    public void PointKey_ExplicitParams_AreCommaJoined()
    {
        var key = PointKeyHasher.Compute(Point(new[] { "System.Int32", "System.String" }), "Ns.Class", "1.0.0-2.0.0");

        Assert.That(key, Is.EqualTo(FnvOf("Asm|Ns.Class|Method|System.Int32,System.String|1.0.0-2.0.0|F|M")));
    }

    [Test]
    public void PointKey_ZeroParamsDiffersFromOverloadWide()
    {
        Assert.That(PointKeyHasher.Compute(Point(Array.Empty<string>()), "Ns.Class", ""), Is.Not.EqualTo(PointKeyHasher.Compute(Point(), "Ns.Class", "")));
    }

    [Test]
    public void PointKey_FactoryOrMetricChange_Differs()
    {
        var baseline = PointKeyHasher.Compute(Point(), "Ns.Class", "");

        Assert.That(PointKeyHasher.Compute(Point(factory: "Other"), "Ns.Class", ""), Is.Not.EqualTo(baseline));
        Assert.That(PointKeyHasher.Compute(Point(metric: "Other"), "Ns.Class", ""), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void PointKey_RangeOrClassChange_Differs()
    {
        var baseline = PointKeyHasher.Compute(Point(), "Ns.Class", "");

        Assert.That(PointKeyHasher.Compute(Point(), "Ns.Class", "1.0.0-2.0.0"), Is.Not.EqualTo(baseline));
        Assert.That(PointKeyHasher.Compute(Point(), "Ns.Other", ""), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void PointKey_NullFactoryEqualsDefaultFactoryName()
    {
        var withNull = PointKeyHasher.Compute(Point(factory: null), "Ns.Class", "");
        var withDefault = PointKeyHasher.Compute(Point(factory: InstrumentationXmlParser.DefaultTracerFactoryName), "Ns.Class", "");

        Assert.That(withNull, Is.EqualTo(withDefault));
    }

    [Test]
    public void PointKey_NullMetricEqualsEmpty()
    {
        Assert.That(PointKeyHasher.Compute(Point(metric: null), "Ns.Class", ""), Is.EqualTo(PointKeyHasher.Compute(Point(metric: ""), "Ns.Class", "")));
    }

    [Test]
    public void PointKey_NullAssemblyAndMethodNames_HashAsEmpty()
    {
        var point = new XmlInstrumentationPoint("F", "M", null, "Ns.Class", null, null);

        Assert.That(PointKeyHasher.Compute(point, "Ns.Class", ""), Is.EqualTo(FnvOf("|Ns.Class||*||F|M")));
    }

    [Test]
    public void PointKey_NonAsciiNames_HashTheirUtf8Bytes()
    {
        var point = new XmlInstrumentationPoint("F", "M", "Asm", "Ns.Cläss", "Méthod😀", null);

        Assert.That(PointKeyHasher.Compute(point, point.ClassName, ""), Is.EqualTo(FnvOf("Asm|Ns.Cläss|Méthod😀|*||F|M")));
    }

    private static ulong FnvOf(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}
