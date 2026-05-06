// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NUnit.Framework;

namespace ParsingTests;

[TestFixture]
public class SqlMetadataCommentBuilderTests
{
    // BuildComment tests

    [Test]
    public void BuildComment_ValidGuid_ReturnsCorrectFormat()
    {
        var result = SqlMetadataCommentBuilder.BuildComment("abc123");

        Assert.That(result, Is.EqualTo("/*nr_service_guid=\"abc123\"*/"));
    }

    [Test]
    public void BuildComment_NullGuid_ReturnsEmpty()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(null);

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void BuildComment_EmptyGuid_ReturnsEmpty()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(string.Empty);

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void BuildComment_GuidContainsStarSlash_ReturnsEmpty()
    {
        var result = SqlMetadataCommentBuilder.BuildComment("injected*/DROP TABLE users");

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void BuildComment_GuidIsQuotedInOutput()
    {
        var result = SqlMetadataCommentBuilder.BuildComment("my-guid");

        Assert.That(result, Does.StartWith("/*nr_service_guid=\""));
        Assert.That(result, Does.EndWith("\"*/"));
    }

    // PrependCommentToSql tests

    [Test]
    public void PrependCommentToSql_NoExistingComment_PrependsComment()
    {
        var result = SqlMetadataCommentBuilder.PrependCommentToSql(
            "SELECT 1", "/*nr_service_guid=\"abc123\"*/");

        Assert.That(result, Is.EqualTo("/*nr_service_guid=\"abc123\"*/SELECT 1"));
    }

    [Test]
    public void PrependCommentToSql_SqlHasNonNrComment_StillPrepends()
    {
        var result = SqlMetadataCommentBuilder.PrependCommentToSql(
            "/*this is a test*/ select name from users",
            "/*nr_service_guid=\"abc123\"*/");

        Assert.That(result, Is.EqualTo("/*nr_service_guid=\"abc123\"*//*this is a test*/ select name from users"));
    }

    [Test]
    public void PrependCommentToSql_SqlAlreadyHasNrServiceGuidPrefix_DoesNotPrepend()
    {
        var original = "/*nr_service_guid=\"old-guid\"*/SELECT 1";
        var result = SqlMetadataCommentBuilder.PrependCommentToSql(
            original, "/*nr_service_guid=\"new-guid\"*/");

        Assert.That(result, Is.SameAs(original));
    }

    [Test]
    public void PrependCommentToSql_EmptyComment_ReturnsSqlUnmodified()
    {
        var original = "SELECT 1";
        var result = SqlMetadataCommentBuilder.PrependCommentToSql(original, string.Empty);

        Assert.That(result, Is.SameAs(original));
    }

    [Test]
    public void PrependCommentToSql_NullComment_ReturnsSqlUnmodified()
    {
        var original = "SELECT 1";
        var result = SqlMetadataCommentBuilder.PrependCommentToSql(original, null);

        Assert.That(result, Is.SameAs(original));
    }
}
