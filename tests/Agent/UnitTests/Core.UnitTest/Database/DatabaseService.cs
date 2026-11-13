// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Database.UnitTest;

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

        [Test]
        public void InlineSqlParameters_UsesTheVendorsDialect_ToInlineAndProtectLiteralsAndComments()
        {
            // Delegates to SqlLiteralScanner.InlineParameters with the vendor's dialect -- full coverage of the
            // scanning/substitution rules themselves lives in SqlLiteralScannerTests; this just confirms the
            // wiring. The inputs are chosen so the test actually fails if ForVendor wired MSSQL to the wrong
            // dialect, rather than merely passing either way:
            //  - "[@second]" is only a protected, verbatim bracket-quoted identifier under MsSql
            //    (SqlDialectFeatures.BracketIdentifiers); under Default it would just be an ordinary "[" followed
            //    by a real "@second" reference, which the dictionary below CAN match ("second" is a real key),
            //    so a wrong dialect would wrongly substitute it.
            //  - "@item#2" is only captured as one whole token under MsSql
            //    (SqlDialectFeatures.HashInIdentifiers, added for exactly this); under Default the scan would
            //    stop at "item", substitute that, and leave a dangling literal "#2" behind.
            const string sql = "SELECT * FROM faketable WHERE [name] = @first AND [@second] = 1 AND @item#2 = 2 -- debug: @second";
            var literals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["first"] = "'value'",
                ["second"] = "'WRONG'",
                ["item#2"] = "'x'",
            };

            var result = _databaseService.InlineSqlParameters(sql, DatastoreVendor.MSSQL, literals);

            Assert.That(result, Is.EqualTo("SELECT * FROM faketable WHERE [name] = 'value' AND [@second] = 1 AND 'x' = 2 -- debug: @second"));
        }
    }
}