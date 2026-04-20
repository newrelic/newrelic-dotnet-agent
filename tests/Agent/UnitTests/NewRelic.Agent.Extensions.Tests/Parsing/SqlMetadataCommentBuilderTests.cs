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
    public void BuildComment_SingleKey_ReturnsCorrectFormat()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            ["nr_service"], "my-app", null, null, null);

        Assert.That(result, Is.EqualTo("/*nr_service=\"my-app\"*/"));
    }

    [Test]
    public void BuildComment_MultipleKeys_CombinesIntoSingleComment()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            ["nr_service", "nr_service_guid", "nr_txn", "nr_trace_id"],
            "pet-clinic", "abc123", "txn-guid-456", "trace789");

        Assert.That(result, Is.EqualTo("/*nr_service=\"pet-clinic\",nr_service_guid=\"abc123\",nr_txn=\"txn-guid-456\",nr_trace_id=\"trace789\"*/"));
    }

    [Test]
    public void BuildComment_EmptyKeyList_ReturnsEmpty()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            [], "my-app", "guid", "txnid", "traceid");

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void BuildComment_NullKeyList_ReturnsEmpty()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            null, "my-app", "guid", "txnid", "traceid");

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void BuildComment_NullValue_OmitsKey()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            ["nr_service", "nr_service_guid"],
            "my-app", null, null, null);

        Assert.That(result, Is.EqualTo("/*nr_service=\"my-app\"*/"));
    }

    [Test]
    public void BuildComment_EmptyValue_OmitsKey()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            ["nr_service", "nr_service_guid"],
            "my-app", string.Empty, null, null);

        Assert.That(result, Is.EqualTo("/*nr_service=\"my-app\"*/"));
    }

    [Test]
    public void BuildComment_AllValuesNullOrEmpty_ReturnsEmpty()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            ["nr_service", "nr_service_guid", "nr_txn", "nr_trace_id"],
            null, null, null, null);

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void BuildComment_ValueContainsStarSlash_OmitsKey()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            ["nr_service", "nr_txn"],
            "safe-app", "injected*/DROP TABLE users", null, null);

        Assert.That(result, Is.EqualTo("/*nr_service=\"safe-app\"*/"));
    }

    [Test]
    public void BuildComment_AllValuesContainStarSlash_ReturnsEmpty()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            ["nr_service"],
            "bad*/value", null, null, null);

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void BuildComment_UnrecognizedKey_IsIgnored()
    {
        // Unrecognized keys should not appear in comment; ValidKeys filters them at config load time,
        // but the builder's switch also handles unknown keys gracefully via the _ => null case.
        var result = SqlMetadataCommentBuilder.BuildComment(
            ["nr_service", "unknown_key"],
            "my-app", null, null, null);

        Assert.That(result, Is.EqualTo("/*nr_service=\"my-app\"*/"));
    }

    [Test]
    public void BuildComment_ValuesAreQuoted()
    {
        var result = SqlMetadataCommentBuilder.BuildComment(
            ["nr_service"], "my app", null, null, null);

        Assert.That(result, Does.StartWith("/*nr_service=\""));
        Assert.That(result, Does.EndWith("\"*/"));
    }

    // PrependCommentToSql tests

    [Test]
    public void PrependCommentToSql_NoExistingComment_PrependsComment()
    {
        var result = SqlMetadataCommentBuilder.PrependCommentToSql(
            "SELECT 1", "/*nr_service=\"app\"*/");

        Assert.That(result, Is.EqualTo("/*nr_service=\"app\"*/SELECT 1"));
    }

    [Test]
    public void PrependCommentToSql_SqlHasNonNrComment_StillPrepends()
    {
        var result = SqlMetadataCommentBuilder.PrependCommentToSql(
            "/*this is a test*/ select name from users",
            "/*nr_service=\"my_app\"*/");

        Assert.That(result, Is.EqualTo("/*nr_service=\"my_app\"*//*this is a test*/ select name from users"));
    }

    [Test]
    public void PrependCommentToSql_SqlAlreadyHasNrPrefix_DoesNotPrepend()
    {
        var original = "/*nr_service=\"old-app\"*/SELECT 1";
        var result = SqlMetadataCommentBuilder.PrependCommentToSql(
            original, "/*nr_service=\"new-app\"*/");

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

    // ValidKeys tests

    [Test]
    public void ValidKeys_ContainsAllSpecDefinedKeys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SqlMetadataCommentBuilder.ValidKeys, Does.Contain("nr_service"));
            Assert.That(SqlMetadataCommentBuilder.ValidKeys, Does.Contain("nr_service_guid"));
            Assert.That(SqlMetadataCommentBuilder.ValidKeys, Does.Contain("nr_txn"));
            Assert.That(SqlMetadataCommentBuilder.ValidKeys, Does.Contain("nr_trace_id"));
        });
    }

    [Test]
    public void ValidKeys_DoesNotContainUnknownKeys()
    {
        Assert.That(SqlMetadataCommentBuilder.ValidKeys, Does.Not.Contain("unknown_key"));
    }
}
