// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

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
                _databaseService = new DatabaseService();
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
                Assert.That(obfuscatedSql, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(unobfuscatedSql, Is.Not.Empty);
                    Assert.That(obfuscatedSql, Is.Not.EqualTo(unobfuscatedSql));
                });
            }
        }
    }
}
