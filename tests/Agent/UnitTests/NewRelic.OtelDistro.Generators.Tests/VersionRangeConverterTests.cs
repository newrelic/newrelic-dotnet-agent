// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class VersionRangeConverterTests
{
    private static void AssertBounds(VersionRangeConversion result, int minMajor, int minMinor, int minPatch, int maxMajor, int maxMinor, int maxPatch)
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)result.MinMajor, Is.EqualTo(minMajor));
            Assert.That((int)result.MinMinor, Is.EqualTo(minMinor));
            Assert.That((int)result.MinPatch, Is.EqualTo(minPatch));
            Assert.That((int)result.MaxMajor, Is.EqualTo(maxMajor));
            Assert.That((int)result.MaxMinor, Is.EqualTo(maxMinor));
            Assert.That((int)result.MaxPatch, Is.EqualTo(maxPatch));
        });
    }

    [TestCase("1.0.0.0", "6.0.0.0", 1, 0, 0, 5, 65535, 65535)]
    [TestCase(null, "8.15.10", 0, 0, 0, 8, 15, 9)]
    [TestCase("5.0", null, 5, 0, 0, 65535, 65535, 65535)]
    [TestCase(null, "107.0.0", 0, 0, 0, 106, 65535, 65535)]
    [TestCase(null, "5.1.0", 0, 0, 0, 5, 0, 65535)]
    [TestCase(null, "1", 0, 0, 0, 0, 65535, 65535)]
    [TestCase("4.122.23", null, 4, 122, 23, 65535, 65535, 65535)]
    [TestCase(null, "0.0.1", 0, 0, 0, 0, 0, 0)]
    [TestCase(null, "0.1.0", 0, 0, 0, 0, 0, 65535)]
    [TestCase(null, "65535.65535.65535", 0, 0, 0, 65535, 65535, 65534)]
    public void Convert_ValidRange_ConvertsToInclusiveThreePartBounds(string minVersion, string maxVersion, int minMajor, int minMinor, int minPatch, int maxMajor, int maxMinor, int maxPatch)
    {
        var result = VersionRangeConverter.Convert(minVersion, maxVersion);

        AssertBounds(result, minMajor, minMinor, minPatch, maxMajor, maxMinor, maxPatch);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsDefault, Is.False);
            Assert.That(result.IsEmpty, Is.False);
            Assert.That(result.MinIssue, Is.EqualTo(VersionBoundIssue.None));
            Assert.That(result.MaxIssue, Is.EqualTo(VersionBoundIssue.None));
        });
    }

    [TestCase(null, null)]
    [TestCase("0.0.0.0", "0.0.0.0")]
    [TestCase("0", "0.0")]
    [TestCase("", "")]
    public void Convert_NoBounds_IsDefaultWithoutIssues(string minVersion, string maxVersion)
    {
        var result = VersionRangeConverter.Convert(minVersion, maxVersion);

        AssertBounds(result, 0, 0, 0, 65535, 65535, 65535);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsDefault, Is.True);
            Assert.That(result.IsEmpty, Is.False);
            Assert.That(result.RangeKey, Is.EqualTo(""));
            Assert.That(result.MinIssue, Is.EqualTo(VersionBoundIssue.None));
            Assert.That(result.MaxIssue, Is.EqualTo(VersionBoundIssue.None));
        });
    }

    // AssemblyVersion::Create (Profiler/Common/AssemblyVersion.h:72-127): more than 4 segments (:85), a std::stoi failure
    // or int overflow (:91-103), a negative (:106) or a value over 65535 (:114) all return null with a warning, which
    // means no bound. Blank text is a stoi failure (no digits), unlike the empty string (:74), which is silently null.
    [TestCase("1.2.3.4.5")]
    [TestCase("70000")]
    [TestCase("1.70000")]
    [TestCase("-1")]
    [TestCase("1.2.-3.4")]
    [TestCase("a")]
    [TestCase("1.a.3")]
    [TestCase("1..2")]
    [TestCase("1.")]
    [TestCase(".1")]
    [TestCase("2147483648")]
    [TestCase("-2147483648")]
    [TestCase("99999999999999999999")]
    [TestCase("-99999999999999999999")]
    [TestCase("   ")]
    [TestCase("-")]
    [TestCase("+")]
    public void Convert_InvalidMinVersion_MeansNoBoundAndInvalidIssue(string invalid)
    {
        var result = VersionRangeConverter.Convert(invalid, null);

        AssertBounds(result, 0, 0, 0, 65535, 65535, 65535);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsDefault, Is.True);
            Assert.That(result.MinIssue, Is.EqualTo(VersionBoundIssue.Invalid));
            Assert.That(result.MaxIssue, Is.EqualTo(VersionBoundIssue.None));
        });
    }

    [TestCase("1.2.3.4.5")]
    [TestCase("70000")]
    [TestCase("-1")]
    [TestCase("a")]
    [TestCase("   ")]
    public void Convert_InvalidMaxVersion_MeansNoBoundAndInvalidIssue(string invalid)
    {
        var result = VersionRangeConverter.Convert(null, invalid);

        AssertBounds(result, 0, 0, 0, 65535, 65535, 65535);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsDefault, Is.True);
            Assert.That(result.MinIssue, Is.EqualTo(VersionBoundIssue.None));
            Assert.That(result.MaxIssue, Is.EqualTo(VersionBoundIssue.Invalid));
        });
    }

    [Test]
    public void Convert_InvalidBoundDoesNotAffectTheOtherBound()
    {
        var result = VersionRangeConverter.Convert("2.0", "oops");

        AssertBounds(result, 2, 0, 0, 65535, 65535, 65535);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsDefault, Is.False);
            Assert.That(result.MinIssue, Is.EqualTo(VersionBoundIssue.None));
            Assert.That(result.MaxIssue, Is.EqualTo(VersionBoundIssue.Invalid));
        });
    }

    // std::stoi, which AssemblyVersion::Create applies to each segment (xplat.h:98), parses a numeric prefix: it skips
    // leading whitespace, takes an optional sign and ignores trailing text. VersionTest.cpp version_validity_test pins
    // the same acceptance ("1x2x3" is 1, "  1.2.3", "1.+2.+3"), so the converter accepts these too; otherwise the
    // distro would disagree with agent mode about which versions a range covers. This includes "1.2.3-beta", which
    // the AssemblyVersion.h doc comment calls invalid but which stoi reads as 1.2.3.
    [TestCase("1.2.3-beta", 1, 2, 3)]
    [TestCase("1x2x3", 1, 0, 0)]
    [TestCase("  1.2.3", 1, 2, 3)]
    [TestCase("1.  2.3", 1, 2, 3)]
    [TestCase("1.2.3   ", 1, 2, 3)]
    [TestCase("\t1\n.\v2\f.\r3", 1, 2, 3)]
    [TestCase("1.+2.+3", 1, 2, 3)]
    [TestCase("12abcd.35wjkg.88sldkjg", 12, 35, 88)]
    [TestCase("01", 1, 0, 0)]
    [TestCase("5.01.340", 5, 1, 340)]
    [TestCase("1.0xff", 1, 0, 0)]
    [TestCase("-0.1", 0, 1, 0)]
    public void Convert_MinVersion_ParsesNumericPrefixesLikeStoi(string minVersion, int major, int minor, int patch)
    {
        var result = VersionRangeConverter.Convert(minVersion, null);

        AssertBounds(result, major, minor, patch, 65535, 65535, 65535);
        Assert.That(result.MinIssue, Is.EqualTo(VersionBoundIssue.None));
    }

    [Test]
    public void Convert_MinVersionWithRevision_IsWidenedToThreeParts()
    {
        var result = VersionRangeConverter.Convert("1.2.3.4", null);

        AssertBounds(result, 1, 2, 3, 65535, 65535, 65535);
        Assert.Multiple(() =>
        {
            Assert.That(result.MinIssue, Is.EqualTo(VersionBoundIssue.RevisionWidened));
            Assert.That(result.MaxIssue, Is.EqualTo(VersionBoundIssue.None));
            Assert.That(result.IsEmpty, Is.False);
        });
    }

    [Test]
    public void Convert_MaxVersionWithRevision_IsWidenedToTheSameThreeParts()
    {
        var result = VersionRangeConverter.Convert(null, "1.2.3.4");

        AssertBounds(result, 0, 0, 0, 1, 2, 3);
        Assert.Multiple(() =>
        {
            Assert.That(result.MinIssue, Is.EqualTo(VersionBoundIssue.None));
            Assert.That(result.MaxIssue, Is.EqualTo(VersionBoundIssue.RevisionWidened));
            Assert.That(result.IsEmpty, Is.False);
        });
    }

    [Test]
    public void Convert_MaxVersionZeroZeroZeroRevision_IsZeroAndNotEmpty()
    {
        var result = VersionRangeConverter.Convert(null, "0.0.0.1");

        AssertBounds(result, 0, 0, 0, 0, 0, 0);
        Assert.Multiple(() =>
        {
            Assert.That(result.MaxIssue, Is.EqualTo(VersionBoundIssue.RevisionWidened));
            Assert.That(result.IsEmpty, Is.False);
            Assert.That(result.IsDefault, Is.False);
        });
    }

    [Test]
    public void Convert_MinZeroZeroZeroRevisionAndNoMax_IsWidenedAndStillDefault()
    {
        var result = VersionRangeConverter.Convert("0.0.0.5", null);

        AssertBounds(result, 0, 0, 0, 65535, 65535, 65535);
        Assert.Multiple(() =>
        {
            Assert.That(result.MinIssue, Is.EqualTo(VersionBoundIssue.RevisionWidened));
            Assert.That(result.IsDefault, Is.True);
            Assert.That(result.RangeKey, Is.EqualTo(""));
        });
    }

    [TestCase("6.0", "6.0")]
    [TestCase("2.0", "1.0")]
    [TestCase("1.5", "1.2")]
    [TestCase("1.2.3", "1.2.3")]
    [TestCase("0.0.1", "0.0.1")]
    public void Convert_MinNotBelowMax_IsEmpty(string minVersion, string maxVersion)
    {
        var result = VersionRangeConverter.Convert(minVersion, maxVersion);

        Assert.That(result.IsEmpty, Is.True);
    }

    [Test]
    public void Convert_MaxJustAboveMinWithRevision_IsNotEmpty()
    {
        var result = VersionRangeConverter.Convert("6.0", "6.0.0.1");

        AssertBounds(result, 6, 0, 0, 6, 0, 0);
        Assert.That(result.IsEmpty, Is.False);
    }

    [Test]
    public void Convert_InvalidMaxWithMin_IsNotEmpty()
    {
        // The invalid max is no bound, so min 6.0.0 against 65535.65535.65535 is a real range.
        Assert.That(VersionRangeConverter.Convert("6.0", "6.0.0.0.0").IsEmpty, Is.False);
    }

    [TestCase("1.0.0.0", "6.0.0.0", "1.0.0-5.65535.65535")]
    [TestCase(null, "8.15.10", "0.0.0-8.15.9")]
    [TestCase("5.0", null, "5.0.0-65535.65535.65535")]
    [TestCase(null, null, "")]
    public void Convert_RangeKey_IsDottedMinDashMaxOrEmptyForDefault(string minVersion, string maxVersion, string expected)
    {
        Assert.That(VersionRangeConverter.Convert(minVersion, maxVersion).RangeKey, Is.EqualTo(expected));
    }
}
