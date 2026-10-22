// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using NewRelic.Agent.Extensions.Parsing;
using NUnit.Framework;

namespace ParsingTests;

[TestFixture]
public class SqlMetadataCommentBuilderTests
{
    [Test]
    [TestCase(CommandType.StoredProcedure, ExpectedResult = true)]
    [TestCase(CommandType.Text, ExpectedResult = false)]
    [TestCase(CommandType.TableDirect, ExpectedResult = false)]
    public bool ShouldSkipCommentForCommandType_ReturnsExpectedResult(CommandType commandType)
    {
        return SqlMetadataCommentBuilder.ShouldSkipCommentForCommandType(commandType);
    }

    [TestCase("/*nr_service_guid=\"MXxBUE18MTIz\"*/SELECT 1", 34)]
    [TestCase("/*nr_service_guid=\"abc\"*/", 25)]
    [TestCase("/*nr_service_guid=\"Ab0_-+/=\"*/SELECT 1", 30)]
    [TestCase("/*nr_service_guid=\"a*b\"*/SELECT 1", 0)]
    [TestCase("/*nr_service_guid=\"abc*\"*/SELECT 1", 0)]
    [TestCase("/*nr_service_guid=\"x' OR ssn='123\"*/SELECT 1", 0)]
    [TestCase("/*nr_service_guid=\"ab\ncd\"*/SELECT 1", 0)]
    [TestCase("/*nr_service_guid=\"a{b\"*/SELECT 1", 0)]
    [TestCase("SELECT 1", 0)]
    [TestCase("", 0)]
    [TestCase(null, 0)]
    [TestCase(" /*nr_service_guid=\"abc\"*/SELECT 1", 0)]
    [TestCase("/*NR_SERVICE_GUID=\"abc\"*/SELECT 1", 0)]
    [TestCase("/*nr_service_guid=abc*/SELECT 1", 0)]
    [TestCase("/*nr_service_guid=\"\"*/SELECT 1", 0)]
    [TestCase("/*nr_service_guid=\"abc*/SELECT 1", 0)]
    [TestCase("/*nr_service_guid=\"a\"b\"*/SELECT 1", 0)]
    [TestCase("/*nr_service_guid=\"a*/b\"*/SELECT 1", 0)]
    [TestCase("/*nr_other=\"abc\"*/SELECT 1", 0)]
    public void GetMetadataCommentLength(string sql, int expected)
    {
        Assert.That(SqlMetadataCommentBuilder.GetMetadataCommentLength(sql), Is.EqualTo(expected));
    }

    [TestCase("abc123")]
    [TestCase("MXxBUE18QVBQTElDQVRJT058MTIzNDU2Nzg5")]
    [TestCase("MXxBUE18QVBQTElDQVRJT058MTIz+/=")]
    public void GetMetadataCommentLength_MatchesBuildComment(string entityGuid)
    {
        var comment = SqlMetadataCommentBuilder.BuildComment(entityGuid);
        var sql = SqlMetadataCommentBuilder.PrependCommentToSql("SELECT * FROM t WHERE a = 'b'", comment);

        Assert.That(SqlMetadataCommentBuilder.GetMetadataCommentLength(sql), Is.EqualTo(comment.Length));
    }

    [Test]
    public void GetMetadataCommentLength_GuidOfMaximumLength_IsRecognized()
    {
        var sql = "/*nr_service_guid=\"" + new string('A', 128) + "\"*/SELECT 1";

        Assert.That(SqlMetadataCommentBuilder.GetMetadataCommentLength(sql), Is.EqualTo(150));
    }

    [Test]
    public void GetMetadataCommentLength_GuidLongerThanMaximum_IsRejected()
    {
        var sql = "/*nr_service_guid=\"" + new string('A', 129) + "\"*/SELECT 1";

        Assert.That(SqlMetadataCommentBuilder.GetMetadataCommentLength(sql), Is.EqualTo(0));
    }
}
