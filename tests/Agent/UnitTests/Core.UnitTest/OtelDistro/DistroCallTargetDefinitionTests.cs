// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroCallTargetDefinitionTests
{
    private static string[] SignatureTypes => new[] { "System.Void", "System.Int32" };

    [Test]
    public void Ctor_ShortOverload_SetsPropertiesAndDefaultVersionRange()
    {
        var definition = new DistroCallTargetDefinition("Assembly", "Type", "Method", SignatureTypes, "Integration");

        Assert.Multiple(() =>
        {
            Assert.That(definition.TargetAssembly, Is.EqualTo("Assembly"));
            Assert.That(definition.TargetType, Is.EqualTo("Type"));
            Assert.That(definition.TargetMethod, Is.EqualTo("Method"));
            Assert.That(definition.TargetSignatureTypes, Is.EqualTo(SignatureTypes));
            Assert.That(definition.IntegrationTypeName, Is.EqualTo("Integration"));
            Assert.That(definition.MinMajor, Is.EqualTo((ushort)0));
            Assert.That(definition.MinMinor, Is.EqualTo((ushort)0));
            Assert.That(definition.MinPatch, Is.EqualTo((ushort)0));
            Assert.That(definition.MaxMajor, Is.EqualTo((ushort)65535));
            Assert.That(definition.MaxMinor, Is.EqualTo((ushort)65535));
            Assert.That(definition.MaxPatch, Is.EqualTo((ushort)65535));
        });
    }

    [Test]
    public void Ctor_FullOverload_SetsAllSixVersionComponents()
    {
        var definition = new DistroCallTargetDefinition("Assembly", "Type", "Method", SignatureTypes, "Integration", 1, 2, 3, 4, 5, 6);

        Assert.Multiple(() =>
        {
            Assert.That(definition.MinMajor, Is.EqualTo((ushort)1));
            Assert.That(definition.MinMinor, Is.EqualTo((ushort)2));
            Assert.That(definition.MinPatch, Is.EqualTo((ushort)3));
            Assert.That(definition.MaxMajor, Is.EqualTo((ushort)4));
            Assert.That(definition.MaxMinor, Is.EqualTo((ushort)5));
            Assert.That(definition.MaxPatch, Is.EqualTo((ushort)6));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void Ctor_NullOrEmptyTargetAssembly_Throws(string targetAssembly)
    {
        Assert.Throws<ArgumentException>(() => new DistroCallTargetDefinition(targetAssembly, "Type", "Method", SignatureTypes, "Integration"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Ctor_NullOrEmptyTargetType_Throws(string targetType)
    {
        Assert.Throws<ArgumentException>(() => new DistroCallTargetDefinition("Assembly", targetType, "Method", SignatureTypes, "Integration"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Ctor_NullOrEmptyTargetMethod_Throws(string targetMethod)
    {
        Assert.Throws<ArgumentException>(() => new DistroCallTargetDefinition("Assembly", "Type", targetMethod, SignatureTypes, "Integration"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Ctor_NullOrEmptyIntegrationTypeName_Throws(string integrationTypeName)
    {
        Assert.Throws<ArgumentException>(() => new DistroCallTargetDefinition("Assembly", "Type", "Method", SignatureTypes, integrationTypeName));
    }

    [Test]
    public void Ctor_NullTargetSignatureTypes_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DistroCallTargetDefinition("Assembly", "Type", "Method", null, "Integration"));
    }

    [Test]
    public void Ctor_EmptyTargetSignatureTypes_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DistroCallTargetDefinition("Assembly", "Type", "Method", new string[0], "Integration"));
    }
}
