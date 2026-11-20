// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroInstrumentationSiteTests
{
    [Test]
    public void Ctor_SetsAllProperties()
    {
        var site = new DistroInstrumentationSite("F", "M", 7u, "A", "T", "Meth", "Sig", 42ul);

        Assert.Multiple(() =>
        {
            Assert.That(site.TracerFactoryName, Is.EqualTo("F"));
            Assert.That(site.MetricName, Is.EqualTo("M"));
            Assert.That(site.TracerArguments, Is.EqualTo(7u));
            Assert.That(site.AssemblyName, Is.EqualTo("A"));
            Assert.That(site.TypeName, Is.EqualTo("T"));
            Assert.That(site.MethodName, Is.EqualTo("Meth"));
            Assert.That(site.ArgumentSignature, Is.EqualTo("Sig"));
            Assert.That(site.FunctionId, Is.EqualTo(42ul));
        });
    }

    [Test]
    public void Ctor_TableFields_DefaultToAPluginPointThatIsOn()
    {
        var site = new DistroInstrumentationSite("F", "M", 0u, "A", "T", "Meth", "Sig", 1ul);

        Assert.Multiple(() =>
        {
            Assert.That(site.PointKey, Is.EqualTo(0ul));
            Assert.That(site.SourceFile, Is.EqualTo(-1));
            Assert.That(site.DefaultOff, Is.False);
            Assert.That(site.LogForwarding, Is.False);
        });
    }

    [Test]
    public void Ctor_LogForwarding_IsSet()
    {
        var site = new DistroInstrumentationSite("F", "M", 0u, "A", "T", "Meth", "Sig", 1ul, 0ul, -1, false, logForwarding: true);

        Assert.That(site.LogForwarding, Is.True);
    }

    [Test]
    public void Ctor_TableFields_AreSet()
    {
        var site = new DistroInstrumentationSite("F", "M", 0u, "A", "T", "Meth", "Sig", 1ul, 0x8000000000000005ul, 4, true, false);

        Assert.Multiple(() =>
        {
            Assert.That(site.PointKey, Is.EqualTo(0x8000000000000005ul));
            Assert.That(site.SourceFile, Is.EqualTo(4));
            Assert.That(site.DefaultOff, Is.True);
        });
    }

    [Test]
    public void Ctor_NullArgumentSignature_BecomesEmptyString()
    {
        var site = new DistroInstrumentationSite("F", "M", 0u, "A", "T", "Meth", null, 1ul);

        Assert.That(site.ArgumentSignature, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Ctor_NullMetricName_IsAllowed()
    {
        var site = new DistroInstrumentationSite("F", null, 0u, "A", "T", "Meth", "", 1ul);

        Assert.That(site.MetricName, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    public void Ctor_NullOrEmptyTracerFactoryName_Throws(string tracerFactoryName)
    {
        Assert.Throws<ArgumentException>(() => new DistroInstrumentationSite(tracerFactoryName, "M", 0u, "A", "T", "Meth", "", 1ul));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Ctor_NullOrEmptyAssemblyName_Throws(string assemblyName)
    {
        Assert.Throws<ArgumentException>(() => new DistroInstrumentationSite("F", "M", 0u, assemblyName, "T", "Meth", "", 1ul));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Ctor_NullOrEmptyTypeName_Throws(string typeName)
    {
        Assert.Throws<ArgumentException>(() => new DistroInstrumentationSite("F", "M", 0u, "A", typeName, "Meth", "", 1ul));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Ctor_NullOrEmptyMethodName_Throws(string methodName)
    {
        Assert.Throws<ArgumentException>(() => new DistroInstrumentationSite("F", "M", 0u, "A", "T", methodName, "", 1ul));
    }
}
