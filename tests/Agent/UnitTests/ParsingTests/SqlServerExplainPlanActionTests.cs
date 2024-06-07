// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using NewRelic.Parsing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
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

            Assert.That(explainPlan, Is.Null);
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

            Assert.That(explainPlan, Is.Null);
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
}
