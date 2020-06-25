/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Database.UnitTest
{
    public class Class_DatabaseService
    {

        [TestFixture, Category("JustMock")]
        public class Request_GetObfuscatedSql
        {
            private DatabaseService _databaseService;

            [SetUp]
            public void Setup()
            {
                _databaseService = new DatabaseService(Mock.Create<ICacheStatsReporter>());
            }

            [TearDown]
            public void Teardown()
            {
                _databaseService.Dispose();
            }

            [Test]
            public void when_connected_then_responds_to_GetObfuscatedSqlRequest()
            {
                // ARRANGE
                const string unobfuscatedSql = "select foo from bar where credit_card=123456789";

                // ACT
                var obfuscatedSql = _databaseService.GetObfuscatedSql(unobfuscatedSql, DatastoreVendor.MSSQL);

                // ASSERT
                Assert.IsNotNull(obfuscatedSql);
                Assert.IsNotEmpty(unobfuscatedSql);
                Assert.AreNotEqual(unobfuscatedSql, obfuscatedSql);
            }
        }
    }
}
