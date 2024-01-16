// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using NewRelic.Parsing;
using NewRelic.Testing.Assertions;
using Telerik.JustMock;

namespace ParsingTests
{
    [TestFixture]
    public class SqlServerExplainPlanActionTests
    {
        [Test]
        public void GenerateExplainPlanReturnsNullIfCommandIsNotIDbCommand()
        {
            var invalidCommand = new object();

            var explainPlan = SqlServerExplainPlanActions.GenerateExplainPlan(invalidCommand);

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

            var explainPlan = SqlServerExplainPlanActions.GenerateExplainPlan(command);

            ClassicAssert.IsNull(explainPlan);
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

            var explainPlan = SqlServerExplainPlanActions.GenerateExplainPlan(command);

            var expectedCreatedCommands = new[]
            {
                "SET SHOWPLAN_ALL ON",
                "SET SHOWPLAN_ALL OFF"
            };
            var actualCreatedCommands = mockConnection.CreatedMockCommands.Select(c => c.CommandText);

            NrAssert.Multiple(
                () => ClassicAssert.IsNotNull(explainPlan, "An explain plan should be returned."),
                () => ClassicAssert.AreEqual("EXEC mystoredproc", command.CommandText, "Expected the command text to not be modified."),
                () => CollectionAssert.AreEquivalent(expectedCreatedCommands, actualCreatedCommands, "Expected 2 commands for enabling and disabling explain plans for the connection."),
                () => CollectionAssert.AreEquivalent(new[] { 0, 1 }, explainPlan.ObfuscatedHeaders, "Expected the first 2 headers to be obfuscated."),
                () => CollectionAssert.AreEquivalent(expectedHeaders, explainPlan.ExplainPlanHeaders, "Expected the headers collections to match."),
                () => ClassicAssert.AreEqual(1, explainPlan.ExplainPlanDatas.Count, "Expected only 1 row of data for the explain plain."),
                () => CollectionAssert.AreEquivalent(new[] {"value0", "value1", "value2"}, explainPlan.ExplainPlanDatas[0], "Expected the explain plan results to match.")
            );
        }

        [Test]
        public void ShouldNotAllocateResourcesIfNotCloneable()
        {
            var mockDbCommand = Mock.Create<IDbCommand>();

            var resources = SqlServerExplainPlanActions.AllocateResources(mockDbCommand);

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

            var resources = SqlServerExplainPlanActions.AllocateResources(mockDbCommand);

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
            var indexes = SqlServerExplainPlanActions.GetObfuscatedIndexes(new List<string>());

            CollectionAssert.IsEmpty(indexes);
        }

        [Test]
        public void GetObfuscatedIndexesShouldBeEmpty_WithHeaders()
        {
            var indexes = SqlServerExplainPlanActions.GetObfuscatedIndexes(new List<string> { "header1", "header2", "header3" });

            CollectionAssert.IsEmpty(indexes);
        }

        [Test]
        public void GetObfuscatedIndexesShouldHaveIndexesForExpectedHeaders()
        {
            var indexes = SqlServerExplainPlanActions.GetObfuscatedIndexes(new List<string> { "header1", "StmtText", "header3", "Argument" });

            CollectionAssert.AreEquivalent(new[] { 1, 3 }, indexes, "Expected the indexes for all headers that should be obfuscated to be returned.");
        }
    }
}
