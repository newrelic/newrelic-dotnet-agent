// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core.Attributes.Tests;

[TestFixture]
public class AttributeValueConverterTests
{
    [Test]
    public void TryConvert_NullInput_ReturnsFalse()
    {
        var result = AttributeValueConverter.TryConvert<string, string>(null, s => s, null, out var output);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(output, Is.Null);
        });
    }

    [Test]
    public void TryConvert_NullConvert_ReturnsFalse()
    {
        var result = AttributeValueConverter.TryConvert<string, string>("a", null, null, out var output);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(output, Is.Null);
        });
    }

    [Test]
    public void TryConvert_ConvertReturnsNull_ReturnsFalse()
    {
        var result = AttributeValueConverter.TryConvert<string, string>("a", _ => null, s => s, out var output);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(output, Is.Null);
        });
    }

    [Test]
    public void TryConvert_PostProcessReturnsNull_ReturnsFalse()
    {
        var result = AttributeValueConverter.TryConvert<string, string>("a", s => s, _ => null, out var output);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(output, Is.Null);
        });
    }

    [Test]
    public void TryConvert_NoPostProcess_ReturnsConvertedValue()
    {
        var result = AttributeValueConverter.TryConvert<int, string>(5, i => i.ToString(), null, out var output);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(output, Is.EqualTo("5"));
        });
    }

    [Test]
    public void TryConvert_WithPostProcess_ReturnsPostProcessedValue()
    {
        var result = AttributeValueConverter.TryConvert<int, string>(5, i => i.ToString(), s => s + "!", out var output);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(output, Is.EqualTo("5!"));
        });
    }
}
