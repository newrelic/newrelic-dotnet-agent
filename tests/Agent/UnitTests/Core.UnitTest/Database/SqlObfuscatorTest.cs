// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Database
{

    [TestFixture]
    public class SqlObfuscatorTest
    {
        SqlObfuscator obfuscator = SqlObfuscator.GetSqlObfuscator("obfuscated");

        [Test]
        public static void verify_using_raw_obfuscator_that_GetObfuscatedSql_returns_sql_passed_in()
        {
            SqlObfuscator ob = SqlObfuscator.GetSqlObfuscator("raw");
            string sql = "Select * from users where ssn = 433871122";
            ClassicAssert.AreEqual(sql, ob.GetObfuscatedSql(sql));
        }

        [Test]
        public static void verify_using_raw_obfuscator_and_quoted_string_in_sql_that_GetObfuscatedSql_returns_sql_passed_in()
        {
            SqlObfuscator ob = SqlObfuscator.GetSqlObfuscator("raw");
            string sql = "Select * from users where name = 'dude'";
            ClassicAssert.AreEqual(sql, ob.GetObfuscatedSql(sql));
        }

        [Test]
        public static void verify_using_NoSql_objfuscator_that_GetObfuscatedSql_returns_null()
        {
            SqlObfuscator ob = SqlObfuscator.GetSqlObfuscator("off");
            string sql = "Select * from users where ssn = 433871122";
            ClassicAssert.IsNull(ob.GetObfuscatedSql(sql));
        }

        [Test]
        public static void verify_using_NoSql_objfuscator_and_quoted_string_in_sql_that_GetObfuscatedSql_returns_null()
        {
            SqlObfuscator ob = SqlObfuscator.GetSqlObfuscator("off");
            string sql = "Select * from users where name = 'dude'";
            ClassicAssert.IsNull(ob.GetObfuscatedSql(sql));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_id_with_questionmark()
        {
            ClassicAssert.AreEqual("Select * from users where ssn = ?",
                obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122"));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_id_with_questionmark_when_datastore_vendor_is_MSSQL()
        {
            ClassicAssert.AreEqual("Select * from users where ssn = ?",
                obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122", DatastoreVendor.MSSQL));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_id_with_questionmark_having_multi_parameters()
        {
            ClassicAssert.AreEqual("Select * from users where ssn = ? and True",
                obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122 and True"));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_id_with_questionmark_having_multi_parameters_when_datastore_vendor_is_MSSQL()
        {
            ClassicAssert.AreEqual("Select * from users where ssn = ? and True",
                obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122 and True", DatastoreVendor.MSSQL));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_decimal_no_remainder_with_questionmark_having_multi_parameters()
        {
            ClassicAssert.AreEqual("Select * from users where number = ? and True",
                obfuscator.GetObfuscatedSql("Select * from users where number = 3. and True"));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_decimal_no_remainder_with_questionmark_having_multi_parameters_when_datastore_vendor_is_MSSQL()
        {
            ClassicAssert.AreEqual("Select * from users where number = ? and True",
                obfuscator.GetObfuscatedSql("Select * from users where number = 3. and True", DatastoreVendor.MSSQL));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_decimal_with_questionmark_having_multi_parameters()
        {
            ClassicAssert.AreEqual("Select * from users where number = ? and True",
                obfuscator.GetObfuscatedSql("Select * from users where number = 3.14159 and True"));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_decimal_with_questionmark_having_multi_parameters_when_datastore_vendor_is_MSSQL()
        {
            ClassicAssert.AreEqual("Select * from users where number = ? and True",
                obfuscator.GetObfuscatedSql("Select * from users where number = 3.14159 and True", DatastoreVendor.MSSQL));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_string_with_int_and_ticks_with_questionmark_having_multi_parameters()
        {
            ClassicAssert.AreEqual("Select * from users where number = ?food? and True",
                obfuscator.GetObfuscatedSql("Select * from users where number = 3.14food'' and True"));

        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_string_with_ticks_with_questionmark()
        {
            ClassicAssert.AreEqual("Select * from users where name = ?",
                obfuscator.GetObfuscatedSql("Select * from users where name = 'dude'"));

        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_string_with_ticks_with_questionmark_when_datastore_vendor_is_MSSQL()
        {
            ClassicAssert.AreEqual("Select * from users where name = ?",
                obfuscator.GetObfuscatedSql("Select * from users where name = 'dude'", DatastoreVendor.MSSQL));

        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_multi_string_with_escapes_with_questionmarks()
        {
            ClassicAssert.AreEqual("Select * from users where name = ???",
                obfuscator.GetObfuscatedSql("Select * from users where name = 'dude''fude'\"bube\""));

        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_string_having_escapes_with_questionmarks()
        {
            ClassicAssert.AreEqual("Select * from users where name = ?",
                obfuscator.GetObfuscatedSql("Select * from users where name = \"dude\""));

        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_string_and_date_having_escapes_with_questionmarks()
        {
            ClassicAssert.AreEqual("Select * from users where name = ? and dob = ? ",
                obfuscator.GetObfuscatedSql("Select * from users where name = \"dude\" and dob = '10/31/1955' "));

        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_string_having_ticks_with_questionmark()
        {
            ClassicAssert.AreEqual("Select * from users where name = ?",
                obfuscator.GetObfuscatedSql("Select * from users where name = 'Sacksman D\\'iablo'"));

        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_string_having_ticks_and_escapes_with_questionmark()
        {
            ClassicAssert.AreEqual("Select * from users where name = ?",
                obfuscator.GetObfuscatedSql("Select * from users where name = \"Adouble\\\"Quote\""));

        }

        [Test, Description("Tests handling a query which is only valid in MS SQL")]
        public void verify_GetObfuscatedSql_ignores_backslashes_when_datastore_vendor_is_MSSQL()
        {
            ClassicAssert.AreEqual("Select * from users where name = ?",
                obfuscator.GetObfuscatedSql(@"Select * from users where name = 'foo\''bar'", DatastoreVendor.MSSQL));

        }

        [Test, Description("Tests handling a query which is only valid in MS SQL")]
        public void verify_GetObfuscatedSql_handles_literal_single_quote_when_datastore_vendor_is_MSSQL()
        {
            ClassicAssert.AreEqual("Select * from users where name = ?",
                obfuscator.GetObfuscatedSql(@"Select * from users where name = 'Sacksman D''iablo'", DatastoreVendor.MSSQL));

        }

        [Test, Description("Tests handling a query which is only valid in MS SQL")]
        public void verify_GetObfuscatedSql_handles_literal_double_quotes_when_datastore_vendor_is_MSSQL()
        {
            ClassicAssert.AreEqual("Select * from users where name = ?",
                obfuscator.GetObfuscatedSql(@"Select * from users where name = ""Quoty O""""Quoterson""", DatastoreVendor.MSSQL));

        }

        [Test]
        public void TestObfuscationFromGibberish()
        {
            string stim = "qrx *().<'\"mumblefrob";  // That's an unterminated single quoted string.
            ClassicAssert.AreEqual("qrx *().<?", obfuscator.GetObfuscatedSql(stim));
        }

        [Test]
        public void TestNumbersInTableNames1()
        {
            ClassicAssert.AreEqual("Select * from users22 where ssn = ?",
                obfuscator.GetObfuscatedSql("Select * from users22 where ssn = 433871122"));
        }

        [Test]
        public void TestNumbersInTableNames2()
        {
            string expect = "SELECT [T1].startDate AS [startDate1], [T2].startDate AS [startDate2] FROM Foo AS [T1] " +
                "INNER JOIN Bar AS [T2] ON [T1].someId = [T2].someId and [T1].id in (?)";
            string stimul = "SELECT [T1].startDate AS [startDate1], [T2].startDate AS [startDate2] FROM Foo AS [T1] " +
                "INNER JOIN Bar AS [T2] ON [T1].someId = [T2].someId and [T1].id in (5)";
            ClassicAssert.AreEqual(expect, obfuscator.GetObfuscatedSql(stimul));
        }

        [Test]
        public void TestSqlObfuscationNumberIn()
        {
            ClassicAssert.AreEqual("Select * from users where ssn in (?) ",
                obfuscator.GetObfuscatedSql("Select * from users where ssn in (666666666) "));
        }

        [Test]
        public void TestSqlObfuscationNumberLessThan()
        {
            ClassicAssert.AreEqual("Select * from users where salary < ?\n",
                obfuscator.GetObfuscatedSql("Select * from users where salary < 12345\n"));
        }

        [Test]
        public void TestSqlObfuscationNumberGreaterThan()
        {
            ClassicAssert.AreEqual("Select * from users where salary > ?",
                obfuscator.GetObfuscatedSql("Select * from users where salary > 12345"));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_numbers_on_left_of_equals_sign_with_a_question_mark()
        {
            ClassicAssert.AreEqual("Select * from users where ? = ssn",
                obfuscator.GetObfuscatedSql("Select * from users where 666666 = ssn"));
        }

        [Test]
        public void verify_GetObfuscatedSql_replaces_numbers_on_right_of_equals_sign_with_a_question_mark()
        {
            ClassicAssert.AreEqual("Select * from users where ssn = ?",
                obfuscator.GetObfuscatedSql("Select * from users where ssn = 7777777"));
        }

    }
}
