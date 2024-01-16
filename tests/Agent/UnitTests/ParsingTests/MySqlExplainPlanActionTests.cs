// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;
using NewRelic.Testing.Assertions;
using Telerik.JustMock;

namespace ParsingTests
{
    [TestFixture]
    public class MySqlExplainPlanActionTests
    {
        [Test]
        public void ShouldNotGenerateExplainPlansForNonSelectStatements()
        {
            var rawUpdateSql = "UPDATE value = @value FROM foo WHERE Id = @id";
            var parsedUpdateStatement = new ParsedSqlStatement(DatastoreVendor.MySQL, "foo", "update");

            ShouldGenerateExplainPlanShouldHaveExpectedResult(rawUpdateSql, parsedUpdateStatement, false, "Will not run EXPLAIN on non-select statements. ");
        }

        [Test]
        public void ShouldGenerateExplainPlansForSingleSelectStatements()
        {
            var rawSelectSql = "SELECT * FROM foo WHERE Id = @id";
            var parsedSelectStatement = new ParsedSqlStatement(DatastoreVendor.MySQL, "foo", "select");

            ShouldGenerateExplainPlanShouldHaveExpectedResult(rawSelectSql, parsedSelectStatement, true, string.Empty);
        }

        [Test]
        public void ShouldNotGenerateExplainPlansForMultipleStatements()
        {
            var rawSql = @"
SELECT @value = value FROM foo WHERE Id = @id;
UPDATE value = @value + 1 FROM foo WHERE Id = @id;";
            var parsedStatement = new ParsedSqlStatement(DatastoreVendor.MySQL, "foo", "select");

            ShouldGenerateExplainPlanShouldHaveExpectedResult(rawSql, parsedStatement, false, "Will not run EXPLAIN on multi-statement SQL query. ");
        }

        [Test]
        public void GenerateExplainPlanReturnsNullIfCommandIsNotIDbCommand()
        {
            var invalidCommand = new object();

            var explainPlan = MySqlExplainPlanActions.GenerateExplainPlan(invalidCommand);

            ClassicAssert.IsNull(explainPlan);
        }

        [Test]
        public void GenerateExplainPlanReturnsNullIfCommandCannotBeFixed()
        {
            var command = new MockDbCommand
            {
                Connection = new MockDbConnection(),
                CommandType = CommandType.Text,
            };

            command.Parameters.Add(new SqlParameter("value", SqlDbType.Binary));

            var explainPlan = MySqlExplainPlanActions.GenerateExplainPlan(command);

            ClassicAssert.IsNull(explainPlan);
        }

        [Test]
        public void GenerateExplainPlanReturnsExplainPlan()
        {
            var command = new MockDbCommand
            {
                Connection = new MockDbConnection(),
                CommandType = CommandType.Text,
                Transaction = Mock.Create<IDbTransaction>(),
                CommandText = "CALL mystoredproc"
            };

            var mockReader = command.MockDataReader;

            Mock.Arrange(() => mockReader.FieldCount).Returns(3);
            Mock.Arrange(() => mockReader.GetName(Arg.IsAny<int>())).Returns<int>(i => $"header{i}");

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

            var explainPlan = MySqlExplainPlanActions.GenerateExplainPlan(command);

            NrAssert.Multiple(
                () => ClassicAssert.IsNotNull(explainPlan, "An explain plan should be returned."),
                () => ClassicAssert.IsNull(command.Transaction, "The transaction should be null."),
                () => StringAssert.StartsWith("EXPLAIN ", command.CommandText),
                () => CollectionAssert.IsEmpty(explainPlan.ObfuscatedHeaders, "Expected there to be no obfuscated headers."),
                () => CollectionAssert.AreEquivalent(new[] {"header0", "header1", "header2"}, explainPlan.ExplainPlanHeaders, "Expected the headers collections to match."),
                () => ClassicAssert.AreEqual(1, explainPlan.ExplainPlanDatas.Count, "Expected only 1 row of data for the explain plain."),
                () => CollectionAssert.AreEquivalent(new[] {"value0", "value1", "value2"}, explainPlan.ExplainPlanDatas[0], "Expected the explain plan results to match.")
            );
        }

        [Test]
        public void ShouldNotAllocateResourcesIfNotCloneable()
        {
            var mockDbCommand = Mock.Create<IDbCommand>();

            var resources = MySqlExplainPlanActions.AllocateResources(mockDbCommand);

            ClassicAssert.IsNull(resources);
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

            var resources = MySqlExplainPlanActions.AllocateResources(mockDbCommand);

            NrAssert.Multiple(
                () => ClassicAssert.IsNotNull(resources, "Expected the new resource to be not null."),
                () => ClassicAssert.AreNotSame(mockDbCommand, resources, "The command is expected to be cloned."),
                () => ClassicAssert.AreNotSame(mockDbCommand.Connection, ((IDbCommand)resources).Connection, "The connection is expected to be cloned."),
                () => ClassicAssert.IsNull(((IDbCommand)resources).Transaction, "The transaction is expected to be null.")
            );
        }

        [Test]
        public void GetObfuscatedIndexesShouldBeEmpty_NoHeaders()
        {
            var indexes = MySqlExplainPlanActions.GetObfuscatedIndexes(new List<string>());

            CollectionAssert.IsEmpty(indexes);
        }

        [Test]
        public void GetObfuscatedIndexesShouldBeEmpty_WithHeaders()
        {
            var indexes = MySqlExplainPlanActions.GetObfuscatedIndexes(new List<string> { "header1", "header2", "header3" });

            CollectionAssert.IsEmpty(indexes);
        }

        private static void ShouldGenerateExplainPlanShouldHaveExpectedResult(string rawSql, ParsedSqlStatement parsedSql, bool expectedIsValid, string expectedMessage)
        {
            var result = MySqlExplainPlanActions.ShouldGenerateExplainPlan(rawSql, parsedSql);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual(expectedIsValid, result.IsValid, "Expected the validation result IsValid property to match the expected value."),
                () => ClassicAssert.AreEqual(expectedMessage, result.ValidationMessage, "Expected the validation result ValidationMessage property to match the expected value.")
            );
        }
    }
}
