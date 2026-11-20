// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class XmlContentHasherTests
{
    [Test]
    public void Compute_BomAndCrlf_EqualsPlainLf()
    {
        var plain = XmlContentHasher.Compute("<a>\n  <b/>\n</a>\n");
        var resaved = XmlContentHasher.Compute("﻿<a>\r\n  <b/>\r\n</a>\r\n");

        Assert.That(resaved, Is.EqualTo(plain));
    }

    [Test]
    public void Compute_BomOnly_EqualsPlain()
    {
        Assert.That(XmlContentHasher.Compute("﻿<a/>"), Is.EqualTo(XmlContentHasher.Compute("<a/>")));
    }

    [Test]
    public void Compute_CrlfOnly_EqualsPlainLf()
    {
        Assert.That(XmlContentHasher.Compute("<a>\r\n</a>"), Is.EqualTo(XmlContentHasher.Compute("<a>\n</a>")));
    }

    [Test]
    public void Compute_BomNotAtStart_IsKept()
    {
        Assert.That(XmlContentHasher.Compute("<a/>﻿"), Is.Not.EqualTo(XmlContentHasher.Compute("<a/>")));
    }

    [Test]
    public void Compute_DifferentText_Differs()
    {
        Assert.That(XmlContentHasher.Compute("<a enabled=\"true\"/>"), Is.Not.EqualTo(XmlContentHasher.Compute("<a enabled=\"false\"/>")));
    }

    [Test]
    public void Compute_EmptyString_IsFnvOffsetBasis()
    {
        Assert.That(XmlContentHasher.Compute(string.Empty), Is.EqualTo(0xcbf29ce484222325UL));
    }

    [Test]
    public void Compute_Null_TreatedAsEmpty()
    {
        Assert.That(XmlContentHasher.Compute((string)null), Is.EqualTo(0xcbf29ce484222325UL));
    }

    [Test]
    public void Compute_KnownText_MatchesFnv1a64()
    {
        // FNV-1a 64 of the single byte 'a' (published test vector).
        Assert.That(XmlContentHasher.Compute("a"), Is.EqualTo(0xaf63dc4c8601ec8cUL));
    }

    [Test]
    public void ComputeBytes_Utf8_EqualsText()
    {
        const string text = "<a x=\"ä😀\">\n  <b/>\r\n</a>\r\n";

        Assert.That(XmlContentHasher.Compute(Encoding.UTF8.GetBytes(text)), Is.EqualTo(XmlContentHasher.Compute(text)));
    }

    [Test]
    public void ComputeBytes_BomAndCrlf_EqualsPlainLf()
    {
        var resaved = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("<a>\r\n  <b/>\r\n</a>\r\n")).ToArray();

        Assert.That(XmlContentHasher.Compute(resaved), Is.EqualTo(XmlContentHasher.Compute("<a>\n  <b/>\n</a>\n")));
    }

    [Test]
    public void ComputeBytes_LoneCrAndCrCrLf_MatchTheTextRules()
    {
        const string text = "a\rb\r\r\nc\r";

        Assert.That(XmlContentHasher.Compute(Encoding.UTF8.GetBytes(text)), Is.EqualTo(XmlContentHasher.Compute(text)));
    }

    [Test]
    public void ComputeBytes_BomNotAtStartOrPartial_IsKept()
    {
        Assert.Multiple(() =>
        {
            Assert.That(XmlContentHasher.Compute(new byte[] { (byte)'a', 0xEF, 0xBB, 0xBF }), Is.Not.EqualTo(XmlContentHasher.Compute("a")));
            Assert.That(XmlContentHasher.Compute(new byte[] { 0xEF, 0xBB }), Is.Not.EqualTo(XmlContentHasher.Compute(string.Empty)));
            Assert.That(XmlContentHasher.Compute(new byte[] { 0xEF, 0xBB, (byte)'a' }), Is.Not.EqualTo(XmlContentHasher.Compute("a")));
            Assert.That(XmlContentHasher.Compute(new byte[] { 0xEF, (byte)'a', 0xBF }), Is.Not.EqualTo(XmlContentHasher.Compute("a")));
        });
    }

    [Test]
    public void ComputeBytes_Utf16_DiffersFromText()
    {
        const string text = "<a/>";
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();

        Assert.That(XmlContentHasher.Compute(utf16), Is.Not.EqualTo(XmlContentHasher.Compute(text)));
    }

    [Test]
    public void ComputeBytes_NullEmptyAndBomOnly_AreFnvOffsetBasis()
    {
        Assert.Multiple(() =>
        {
            Assert.That(XmlContentHasher.Compute((byte[])null), Is.EqualTo(0xcbf29ce484222325UL));
            Assert.That(XmlContentHasher.Compute(new byte[0]), Is.EqualTo(0xcbf29ce484222325UL));
            Assert.That(XmlContentHasher.Compute(new byte[] { 0xEF, 0xBB, 0xBF }), Is.EqualTo(0xcbf29ce484222325UL));
        });
    }
}
