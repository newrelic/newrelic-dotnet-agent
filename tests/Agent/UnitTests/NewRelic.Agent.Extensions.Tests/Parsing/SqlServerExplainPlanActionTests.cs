// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.SqlServer.Server;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace ParsingTests;

[TestFixture]
public class SqlServerExplainPlanActionTests
{
    // Real comment-aware stripping lives in Core (NewRelic.Agent.Core.Database.SqlLiteralScanner), which this
    // project cannot reference -- see the class-level notes on InlineParametersForCompileOnlyExplain. None of the
    // commands below contain a comment, so a no-op stand-in exercises these tests identically to before the
    // parameter was added; the comment-stripping wiring itself is covered separately below.
    private static readonly Func<string, string> NoOpStripComments = sql => sql;

    [Test]
    public void GenerateExplainPlanReturnsNullIfCommandIsNotIDbCommand()
    {
        var invalidCommand = new object();

        var explainPlan = SqlServerExplainPlanActions.GenerateExplainPlan(invalidCommand, NoOpStripComments);

        Assert.That(explainPlan, Is.Null);
    }

    [Test]
    public void GenerateExplainPlanReturnsNullIfCommandCannotBeFixed()
    {
        // When InlineParametersForCompileOnlyExplain reports it cannot safely inline (e.g. a Binary parameter),
        // GenerateExplainPlan must bail out before ever touching the connection -- no SET SHOWPLAN_ALL ON/OFF,
        // no cloned command. An empty CreatedMockCommands proves that early return, not just a null result.
        var mockConnection = new MockDbConnection();
        var command = new MockDbCommand
        {
            Connection = mockConnection,
            CommandType = CommandType.Text,
            CommandText = "SELECT * FROM faketable WHERE [name] = @onlyParam"
        };
        command.Parameters.Add(new SqlParameter("@onlyParam", SqlDbType.Binary) { Value = new byte[] { 1, 2, 3 } });

        var explainPlan = SqlServerExplainPlanActions.GenerateExplainPlan(command, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(explainPlan, Is.Null);
            Assert.That(mockConnection.CreatedMockCommands, Is.Empty, "No SET SHOWPLAN_ALL command should have been sent.");
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_StoredProcedure_LeavesCommandUntouched()
    {
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("MyProc", emptyConnection) { CommandType = CommandType.StoredProcedure };
        sqlCommand.Parameters.AddWithValue("@unused", "shouldNeverBeTouched");

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("MyProc"));
            Assert.That(sqlCommand.Parameters, Has.Count.EqualTo(1), "Parameters should not be cleared for stored procedures.");
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_NoParameters_LeavesCommandTextUntouched()
    {
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable", emptyConnection);

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("SELECT * FROM faketable"));
        });
    }

    [TestCase(DbType.Binary)]
    [TestCase(DbType.Object)]
    public void InlineParametersForCompileOnlyExplain_BinaryOrObjectParameter_SkipsExplain(DbType dbType)
    {
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [name] = @onlyParam", emptyConnection);
        sqlCommand.Parameters.Add("@onlyParam", dbType == DbType.Binary ? SqlDbType.Binary : SqlDbType.Structured);
        sqlCommand.Parameters["@onlyParam"].Value = dbType == DbType.Binary ? (object)new byte[] { 1, 2, 3 } : new SqlDataRecord();

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.False);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("SELECT * FROM faketable WHERE [name] = @onlyParam"), "Command text should be untouched when explain is skipped.");
        });
    }

    [TestCase(true, "1")]
    [TestCase(false, "0")]
    public void InlineParametersForCompileOnlyExplain_BooleanParameter_InlinesAsZeroOrOne(bool value, string expectedLiteral)
    {
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [flag] = @onlyParam", emptyConnection);
        sqlCommand.Parameters.Add("@onlyParam", SqlDbType.Bit).Value = value;

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo($"SELECT * FROM faketable WHERE [flag] = {expectedLiteral}"));
            Assert.That(sqlCommand.Parameters, Is.Empty);
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_StringParameterWithEmbeddedQuote_IsEscapedByDoubling()
    {
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [name] = @onlyParam", emptyConnection);
        sqlCommand.Parameters.Add("@onlyParam", SqlDbType.NVarChar).Value = "O'Brien";

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("SELECT * FROM faketable WHERE [name] = 'O''Brien'"));
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_ParameterNameWithoutAtPrefix_InlinesCorrectly()
    {
        // ADO.NET allows a parameter to be named with no leading "@"; the SQL text's placeholder still has one.
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [name] = @onlyParam", emptyConnection);
        sqlCommand.Parameters.Add("onlyParam", SqlDbType.NVarChar).Value = "value";

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("SELECT * FROM faketable WHERE [name] = 'value'"));
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_RegexMetacharacterValue_IsNotReinterpretedAsAReplacementPattern()
    {
        // "a$&b" is special in a .NET regex *replacement string* ($& re-inserts the whole match). Using the
        // MatchEvaluator overload of Regex.Replace, rather than the string overload, must keep this literal.
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [name] = @onlyParam", emptyConnection);
        sqlCommand.Parameters.Add("@onlyParam", SqlDbType.NVarChar).Value = "a$&b";

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("SELECT * FROM faketable WHERE [name] = 'a$&b'"));
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_OneParametersValueMatchingAnothersPlaceholder_IsNotSplicedASecondTime()
    {
        // @first's value is literally the text of @second's placeholder. A correct single-pass substitution
        // must not re-scan @first's already-substituted value and mistake it for a real reference to @second.
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [name] = @first AND [other] = @second", emptyConnection);
        sqlCommand.Parameters.Add("@first", SqlDbType.NVarChar).Value = "@second";
        sqlCommand.Parameters.Add("@second", SqlDbType.NVarChar).Value = "realValue";

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("SELECT * FROM faketable WHERE [name] = '@second' AND [other] = 'realValue'"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void InlineParametersForCompileOnlyExplain_ParameterNameIsPrefixOfAnother_BothInlinedCorrectly(bool shorterParameterAddedFirst)
    {
        // "@id" is a textual prefix of "@id2". The combined regex alternation's trailing \b must stop "@id" from
        // matching inside "@id2" (or vice versa), regardless of which parameter was added -- and therefore
        // iterated -- first when building that alternation.
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [id] = @id AND [otherId] = @id2", emptyConnection);

        if (shorterParameterAddedFirst)
        {
            sqlCommand.Parameters.Add("@id", SqlDbType.Int).Value = 1;
            sqlCommand.Parameters.Add("@id2", SqlDbType.Int).Value = 2;
        }
        else
        {
            sqlCommand.Parameters.Add("@id2", SqlDbType.Int).Value = 2;
            sqlCommand.Parameters.Add("@id", SqlDbType.Int).Value = 1;
        }

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("SELECT * FROM faketable WHERE [id] = 1 AND [otherId] = 2"));
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_NullValue_BecomesUnquotedNullKeyword()
    {
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [name] = @onlyParam", emptyConnection);
        sqlCommand.Parameters.Add("@onlyParam", SqlDbType.NVarChar).Value = null;

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("SELECT * FROM faketable WHERE [name] = NULL"));
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_DbNullValue_BecomesUnquotedNullKeyword()
    {
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [name] = @onlyParam", emptyConnection);
        sqlCommand.Parameters.Add("@onlyParam", SqlDbType.NVarChar).Value = DBNull.Value;

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, NoOpStripComments);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Is.EqualTo("SELECT * FROM faketable WHERE [name] = NULL"));
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_CallsStripSqlCommentsOnTheCommandTextBeforeMatching()
    {
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [name] = @onlyParam", emptyConnection);
        sqlCommand.Parameters.Add("@onlyParam", SqlDbType.NVarChar).Value = "value";

        string observedCommandText = null;
        string StripComments(string sql)
        {
            observedCommandText = sql;
            return sql;
        }

        SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, StripComments);

        Assert.That(observedCommandText, Is.EqualTo("SELECT * FROM faketable WHERE [name] = @onlyParam"),
            "The caller-supplied comment stripper should run against the original command text before placeholder matching.");
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_ParameterNameCoincidentallyInsideAStrippedComment_IsNotMatched()
    {
        // Simulates what a real comment-aware stripper (NewRelic.Agent.Core.Database.SqlLiteralScanner.StripComments
        // in production) would do to a "-- ... @second ..." comment: blank it out before matching. @second's own
        // placeholder never appears in the real SQL, only in a developer comment that happens to mention its name.
        // Comments are stripped before placeholder matching, so a parameter name that happens to appear inside a
        // comment is never treated as a placeholder to fill in -- removing the comment first is simpler than
        // trying to detect and skip placeholder-like text inside it.
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand(
            "SELECT * FROM faketable WHERE [name] = @first -- debug: filtering by @second",
            emptyConnection);
        sqlCommand.Parameters.Add("@first", SqlDbType.NVarChar).Value = "benignValue";
        sqlCommand.Parameters.Add("@second", SqlDbType.NVarChar).Value = "ignoredValue";

        string StripTrailingDashDashComment(string sql)
        {
            var commentStart = sql.IndexOf("--", StringComparison.Ordinal);
            return commentStart < 0 ? sql : sql.Substring(0, commentStart) + new string(' ', sql.Length - commentStart);
        }

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, StripTrailingDashDashComment);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.True);
            Assert.That(sqlCommand.CommandText, Does.StartWith("SELECT * FROM faketable WHERE [name] = 'benignValue'"));
            Assert.That(sqlCommand.CommandText, Does.Not.Contain("ignoredValue"),
                "@second's value must not be inlined anywhere -- its placeholder only ever appeared inside the (now-stripped) comment.");
        });
    }

    [Test]
    public void InlineParametersForCompileOnlyExplain_StripSqlCommentsReturnsNull_AbortsThePlanRatherThanMatchingUntrustworthyText()
    {
        var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
        var sqlCommand = new SqlCommand("SELECT * FROM faketable WHERE [name] = @onlyParam", emptyConnection);
        sqlCommand.Parameters.Add("@onlyParam", SqlDbType.NVarChar).Value = "value";
        var originalCommandText = sqlCommand.CommandText;

        string StripCommentsUnableToClassify(string sql) => null;

        var shouldGeneratePlan = SqlServerExplainPlanActions.InlineParametersForCompileOnlyExplain(sqlCommand, StripCommentsUnableToClassify);

        Assert.Multiple(() =>
        {
            Assert.That(shouldGeneratePlan, Is.False);
            Assert.That(sqlCommand.CommandText, Is.EqualTo(originalCommandText), "The command should be left untouched when the plan is aborted.");
        });
    }

    [Test]
    public void GenerateExplainPlanInlinesParametersSafely_BecauseShowplanCannotSeeInsideAnRpcCall()
    {
        // SET SHOWPLAN_ALL ON only intercepts plain-text batches; a command with bound parameters is sent as an
        // RPC call that SHOWPLAN never sees. So, unlike MySQL, the SQL Server path still needs to inline -- via
        // InlineParametersForCompileOnlyExplain, not the old FixParameterizedSql.
        var command = new MockDbCommand
        {
            Connection = new MockDbConnection(),
            CommandType = CommandType.Text,
            CommandText = "SELECT * FROM foo WHERE Id = @id"
        };
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = 42 });

        var mockReader = command.MockDataReader;
        Mock.Arrange(() => mockReader.FieldCount).Returns(0);
        Mock.Arrange(() => mockReader.Read()).Returns(false);

        var explainPlan = SqlServerExplainPlanActions.GenerateExplainPlan(command, NoOpStripComments);

        NrAssert.Multiple(
            () => Assert.That(explainPlan, Is.Not.Null, "An explain plan should be returned."),
            () => Assert.That(command.CommandText, Is.EqualTo("SELECT * FROM foo WHERE Id = 42"), "The parameter should be inlined into the compile-only batch."),
            () => Assert.That(command.Parameters, Is.Empty, "Parameters should be cleared once inlined.")
        );
    }

    [Test]
    public void GenerateExplainPlanReturnsExplainPlan()
    {
        var mockConnection = new MockDbConnection();
        var command = new MockDbCommand
        {
            Connection = mockConnection,
            CommandType = CommandType.Text,
            CommandText = "EXEC mystoredproc"
        };

        var mockReader = command.MockDataReader;

        var expectedHeaders = new[] { "StmtText", "Argument", "header2" };
        Mock.Arrange(() => mockReader.FieldCount).Returns(3);
        Mock.Arrange(() => mockReader.GetName(Arg.IsAny<int>())).Returns<int>(i => expectedHeaders[i]);

        var nextReaderReturnValue = true;
        Mock.Arrange(() => mockReader.Read()).Returns(() =>
        {
            if (nextReaderReturnValue)
            {
                nextReaderReturnValue = false;
                return true;
            }
            return false;
        });

        Mock.Arrange(() => mockReader.GetValues(Arg.IsAny<object[]>())).Returns<object[]>(values =>
        {
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = $"value{i}";
            }
            return values.Length;
        });

        var explainPlan = SqlServerExplainPlanActions.GenerateExplainPlan(command, NoOpStripComments);

        var expectedCreatedCommands = new[]
        {
            "SET SHOWPLAN_ALL ON",
            "SET SHOWPLAN_ALL OFF"
        };
        var actualCreatedCommands = mockConnection.CreatedMockCommands.Select(c => c.CommandText);

        NrAssert.Multiple(
            () => Assert.That(explainPlan, Is.Not.Null, "An explain plan should be returned."),
            () => Assert.That(command.CommandText, Is.EqualTo("EXEC mystoredproc"), "Expected the command text to not be modified."),
            () => Assert.That(actualCreatedCommands, Is.EquivalentTo(expectedCreatedCommands), "Expected 2 commands for enabling and disabling explain plans for the connection."),
            () => Assert.That(explainPlan.ObfuscatedHeaders, Is.EquivalentTo(new[] { 0, 1 }), "Expected the first 2 headers to be obfuscated."),
            () => Assert.That(explainPlan.ExplainPlanHeaders, Is.EquivalentTo(expectedHeaders), "Expected the headers collections to match."),
            () => Assert.That(explainPlan.ExplainPlanDatas, Has.Count.EqualTo(1), "Expected only 1 row of data for the explain plain."),
            () => Assert.That(explainPlan.ExplainPlanDatas[0], Is.EquivalentTo(new[] {"value0", "value1", "value2"}), "Expected the explain plan results to match.")
        );
    }

    [Test]
    public void GenerateExplainPlan_ReadingResultsThrows_StillTurnsShowplanOffBeforeDisposingTheConnection()
    {
        // A failure while reading the explain plan's results (or ExecuteReader itself) must not skip
        // "SET SHOWPLAN_ALL OFF" -- otherwise the cloned connection's session is disposed/returned to the
        // pool with SHOWPLAN_ALL still ON.
        var mockConnection = new MockDbConnection();
        var command = new MockDbCommand
        {
            Connection = mockConnection,
            CommandType = CommandType.Text,
            CommandText = "SELECT * FROM faketable"
        };

        var mockReader = command.MockDataReader;
        Mock.Arrange(() => mockReader.FieldCount).Throws(new InvalidOperationException("simulated failure reading explain plan results"));

        Assert.That(() => SqlServerExplainPlanActions.GenerateExplainPlan(command, NoOpStripComments), Throws.InvalidOperationException);

        var expectedCreatedCommands = new[]
        {
            "SET SHOWPLAN_ALL ON",
            "SET SHOWPLAN_ALL OFF"
        };
        var actualCreatedCommands = mockConnection.CreatedMockCommands.Select(c => c.CommandText);

        Assert.That(actualCreatedCommands, Is.EquivalentTo(expectedCreatedCommands),
            "SHOWPLAN should still be turned back off even though reading the explain plan results failed.");
    }

    [Test]
    public void GenerateExplainPlan_ReadingResultsThrowsAndTurningShowplanOffAlsoThrows_OriginalExceptionStillPropagates()
    {
        // The finally block's own catch swallows a failure turning SHOWPLAN back off specifically so it doesn't
        // mask the original failure -- this proves that contract holds even when both actually fail.
        var mockConnection = new MockDbConnection
        {
            ThrowOnSecondCreatedCommandExecute = new InvalidOperationException("simulated failure turning SHOWPLAN off")
        };
        var command = new MockDbCommand
        {
            Connection = mockConnection,
            CommandType = CommandType.Text,
            CommandText = "SELECT * FROM faketable"
        };

        var mockReader = command.MockDataReader;
        Mock.Arrange(() => mockReader.FieldCount).Throws(new InvalidOperationException("simulated failure reading explain plan results"));

        Assert.That(() => SqlServerExplainPlanActions.GenerateExplainPlan(command, NoOpStripComments),
            Throws.InvalidOperationException.With.Message.EqualTo("simulated failure reading explain plan results"));
    }

    [Test]
    public void ShouldNotAllocateResourcesIfNotCloneable()
    {
        var mockDbCommand = Mock.Create<IDbCommand>();

        var resources = SqlServerExplainPlanActions.AllocateResources(mockDbCommand);

        Assert.That(resources, Is.Null);
    }

    [Test]
    public void ShouldAllocateResourcesIfCloneable()
    {
        // Using the concrete mock so that ICloneable can be implemented
        var mockDbCommand = new MockDbCommand
        {
            Connection = new MockDbConnection(),
            Transaction = Mock.Create<IDbTransaction>()
        };

        var resources = SqlServerExplainPlanActions.AllocateResources(mockDbCommand);

        NrAssert.Multiple(
            () => Assert.That(resources, Is.Not.Null, "Expected the new resource to be not null."),
            () => Assert.That(resources, Is.Not.SameAs(mockDbCommand), "The command is expected to be cloned."),
            () => Assert.That(((IDbCommand)resources).Connection, Is.Not.SameAs(mockDbCommand.Connection), "The connection is expected to be cloned."),
            () => Assert.That(((IDbCommand)resources).Transaction, Is.Null, "The transaction is expected to be null.")
        );
    }

    [Test]
    public void GetObfuscatedIndexesShouldBeEmpty_NoHeaders()
    {
        var indexes = SqlServerExplainPlanActions.GetObfuscatedIndexes(new List<string>());

        Assert.That(indexes, Is.Empty);
    }

    [Test]
    public void GetObfuscatedIndexesShouldBeEmpty_WithHeaders()
    {
        var indexes = SqlServerExplainPlanActions.GetObfuscatedIndexes(new List<string> { "header1", "header2", "header3" });

        Assert.That(indexes, Is.Empty);
    }

    [Test]
    public void GetObfuscatedIndexesShouldHaveIndexesForExpectedHeaders()
    {
        var indexes = SqlServerExplainPlanActions.GetObfuscatedIndexes(new List<string> { "header1", "StmtText", "header3", "Argument" });

        Assert.That(indexes, Is.EquivalentTo(new[] { 1, 3 }), "Expected the indexes for all headers that should be obfuscated to be returned.");
    }
}