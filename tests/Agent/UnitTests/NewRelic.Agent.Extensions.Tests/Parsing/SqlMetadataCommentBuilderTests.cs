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
}
