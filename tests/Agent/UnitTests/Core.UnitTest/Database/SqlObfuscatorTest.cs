// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Database;

[TestFixture]
public class SqlObfuscatorTest
{
    private const string MetadataComment = "/*nr_service_guid=\"MXxBUE18MTIz\"*/";

    SqlObfuscator obfuscator = SqlObfuscator.GetSqlObfuscator("obfuscated");

    [Test]
    public static void verify_using_raw_obfuscator_that_GetObfuscatedSql_returns_sql_passed_in()
    {
        SqlObfuscator ob = SqlObfuscator.GetSqlObfuscator("raw");
        string sql = "Select * from users where ssn = 433871122";
        Assert.That(ob.GetObfuscatedSql(sql), Is.EqualTo(sql));
    }

    [Test]
    public static void verify_using_raw_obfuscator_and_quoted_string_in_sql_that_GetObfuscatedSql_returns_sql_passed_in()
    {
        SqlObfuscator ob = SqlObfuscator.GetSqlObfuscator("raw");
        string sql = "Select * from users where name = 'dude'";
        Assert.That(ob.GetObfuscatedSql(sql), Is.EqualTo(sql));
    }

    [Test]
    public static void verify_using_NoSql_objfuscator_that_GetObfuscatedSql_returns_null()
    {
        SqlObfuscator ob = SqlObfuscator.GetSqlObfuscator("off");
        string sql = "Select * from users where ssn = 433871122";
        Assert.That(ob.GetObfuscatedSql(sql), Is.Null);
    }

    [Test]
    public static void verify_using_NoSql_objfuscator_and_quoted_string_in_sql_that_GetObfuscatedSql_returns_null()
    {
        SqlObfuscator ob = SqlObfuscator.GetSqlObfuscator("off");
        string sql = "Select * from users where name = 'dude'";
        Assert.That(ob.GetObfuscatedSql(sql), Is.Null);
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_id_with_questionmark()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122"), Is.EqualTo("Select * from users where ssn = ?"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_id_with_questionmark_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where ssn = ?"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_id_with_questionmark_having_multi_parameters()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122 and True"), Is.EqualTo("Select * from users where ssn = ? and True"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_id_with_questionmark_having_multi_parameters_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122 and True", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where ssn = ? and True"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_decimal_no_remainder_with_questionmark_having_multi_parameters()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3. and True"), Is.EqualTo("Select * from users where number = ? and True"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_decimal_no_remainder_with_questionmark_having_multi_parameters_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3. and True", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where number = ? and True"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_decimal_with_questionmark_having_multi_parameters()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3.14159 and True"), Is.EqualTo("Select * from users where number = ? and True"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_decimal_with_questionmark_having_multi_parameters_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3.14159 and True", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where number = ? and True"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_string_with_int_and_ticks_with_questionmark_having_multi_parameters()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3.14food'' and True"), Is.EqualTo("Select * from users where number = ?food? and True"));

    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_string_with_ticks_with_questionmark()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where name = 'dude'"), Is.EqualTo("Select * from users where name = ?"));

    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_string_with_ticks_with_questionmark_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where name = 'dude'", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where name = ?"));

    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_multi_string_with_escapes_with_questionmarks()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where name = 'dude''fude'\"bube\""), Is.EqualTo("Select * from users where name = ???"));

    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_string_having_escapes_with_questionmarks()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where name = \"dude\""), Is.EqualTo("Select * from users where name = ?"));

    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_string_and_date_having_escapes_with_questionmarks()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where name = \"dude\" and dob = '10/31/1955' "), Is.EqualTo("Select * from users where name = ? and dob = ? "));

    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_string_having_ticks_with_questionmark()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where name = 'Sacksman D\\'iablo'"), Is.EqualTo("Select * from users where name = ?"));

    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_string_having_ticks_and_escapes_with_questionmark()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where name = \"Adouble\\\"Quote\""), Is.EqualTo("Select * from users where name = ?"));

    }

    [Test, Description("Tests handling a query which is only valid in MS SQL")]
    public void verify_GetObfuscatedSql_ignores_backslashes_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql(@"Select * from users where name = 'foo\''bar'", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where name = ?"));

    }

    [Test, Description("Tests handling a query which is only valid in MS SQL")]
    public void verify_GetObfuscatedSql_handles_literal_single_quote_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql(@"Select * from users where name = 'Sacksman D''iablo'", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where name = ?"));

    }

    [Test, Description("Tests handling a query which is only valid in MS SQL")]
    public void verify_GetObfuscatedSql_handles_literal_double_quotes_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql(@"Select * from users where name = ""Quoty O""""Quoterson""", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where name = ?"));

    }

    [Test]
    public void TestObfuscationFromGibberish()
    {
        string stim = "qrx *().<'\"mumblefrob";  // That's an unterminated single quoted string.
        Assert.That(obfuscator.GetObfuscatedSql(stim), Is.EqualTo("qrx *().<?"));
    }

    [Test]
    public void TestNumbersInTableNames1()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users22 where ssn = 433871122"), Is.EqualTo("Select * from users22 where ssn = ?"));
    }

    [Test]
    public void TestNumbersInTableNames2()
    {
        string expect = "SELECT [T1].startDate AS [startDate1], [T2].startDate AS [startDate2] FROM Foo AS [T1] " +
                        "INNER JOIN Bar AS [T2] ON [T1].someId = [T2].someId and [T1].id in (?)";
        string stimul = "SELECT [T1].startDate AS [startDate1], [T2].startDate AS [startDate2] FROM Foo AS [T1] " +
                        "INNER JOIN Bar AS [T2] ON [T1].someId = [T2].someId and [T1].id in (5)";
        Assert.That(obfuscator.GetObfuscatedSql(stimul), Is.EqualTo(expect));
    }

    [Test]
    public void TestSqlObfuscationNumberIn()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where ssn in (666666666) "), Is.EqualTo("Select * from users where ssn in (?) "));
    }

    [Test]
    public void TestSqlObfuscationNumberLessThan()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where salary < 12345\n"), Is.EqualTo("Select * from users where salary < ?\n"));
    }

    [Test]
    public void TestSqlObfuscationNumberGreaterThan()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where salary > 12345"), Is.EqualTo("Select * from users where salary > ?"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_numbers_on_left_of_equals_sign_with_a_question_mark()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where 666666 = ssn"), Is.EqualTo("Select * from users where ? = ssn"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_numbers_on_right_of_equals_sign_with_a_question_mark()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where ssn = 7777777"), Is.EqualTo("Select * from users where ssn = ?"));
    }

    [TestCase("SELECT \"t001\".\"c2\" FROM \"t001\" WHERE \"t001\".\"c2\" = 'value' AND c3=1234 LIMIT 1",
              "SELECT \"t001\".\"c2\" FROM \"t001\" WHERE \"t001\".\"c2\" = ? AND c3=? LIMIT ?")]
    [TestCase("/*app:demo*/ SELECT * FROM t WHERE a = 'b'", "? SELECT * FROM t WHERE a = ?")]
    [TestCase("SELECT * FROM t WHERE a = 'unterminated", "?")]
    public void GetObfuscatedSql_Postgres_UsesPostgresDialect(string sql, string expected)
    {
        Assert.That(obfuscator.GetObfuscatedSql(sql, DatastoreVendor.Postgres), Is.EqualTo(expected));
    }

    [Test]
    public void GetObfuscatedSql_Postgres_Null_ReturnsNull()
    {
        Assert.That(obfuscator.GetObfuscatedSql(null, DatastoreVendor.Postgres), Is.Null);
    }

    [TestCase(DatastoreVendor.Postgres)]
    [TestCase(DatastoreVendor.MSSQL)]
    public void GetObfuscatedSql_TruncatesTo16384(DatastoreVendor vendor)
    {
        var sql = "SELECT " + new string('a', 20000);
        Assert.That(obfuscator.GetObfuscatedSql(sql, vendor).Length, Is.EqualTo(16384));
    }

    [TestCase(DatastoreVendor.Postgres)]
    [TestCase(DatastoreVendor.MSSQL)]
    [TestCase(DatastoreVendor.MySQL)]
    [TestCase(DatastoreVendor.Oracle)]
    [TestCase(DatastoreVendor.IBMDB2)]
    [TestCase(DatastoreVendor.Other)]
    public void GetObfuscatedSql_KeepsMetadataComment(DatastoreVendor vendor)
    {
        var result = obfuscator.GetObfuscatedSql(MetadataComment + "SELECT * FROM t WHERE a = 'b'", vendor);
        Assert.That(result, Is.EqualTo(MetadataComment + "SELECT * FROM t WHERE a = ?"));
    }

    [Test]
    public void GetObfuscatedSql_Postgres_KeepsMetadataCommentWhenBodyIsUnterminated()
    {
        Assert.That(obfuscator.GetObfuscatedSql(MetadataComment + "SELECT 'x", DatastoreVendor.Postgres), Is.EqualTo(MetadataComment + "?"));
    }

    [TestCase(DatastoreVendor.Postgres)]
    [TestCase(DatastoreVendor.MSSQL)]
    public void GetObfuscatedSql_KeepsMetadataCommentWhenTruncating(DatastoreVendor vendor)
    {
        var result = obfuscator.GetObfuscatedSql(MetadataComment + "SELECT " + new string('a', 20000), vendor);
        Assert.That(result.Length, Is.EqualTo(16384));
        Assert.That(result, Does.StartWith(MetadataComment + "SELECT aaa"));
    }

    [Test]
    public void GetObfuscatedSql_Postgres_MasksBodyWhenUnterminatedPastTheLimit()
    {
        var body = "SELECT " + new string('a', 20000) + " WHERE b = 'x";
        Assert.That(obfuscator.GetObfuscatedSql(body, DatastoreVendor.Postgres), Is.EqualTo("?"));
        Assert.That(obfuscator.GetObfuscatedSql(MetadataComment + body, DatastoreVendor.Postgres), Is.EqualTo(MetadataComment + "?"));
    }

    [Test]
    public void GetObfuscatedSql_NonPostgres_TruncatesLikeMain()
    {
        var sql = "SELECT " + string.Concat(Enumerable.Repeat("1, ", 10000));
        var expected = ("SELECT " + string.Concat(Enumerable.Repeat("?, ", 10000))).Substring(0, 16384);
        Assert.That(obfuscator.GetObfuscatedSql(sql, DatastoreVendor.MySQL), Is.EqualTo(expected));
    }

    [TestCase(DatastoreVendor.Other, "/*nr_service_guid=\"a\"b\"*/SELECT 1", "/*nr_service_guid=?b?")]
    [TestCase(DatastoreVendor.Other, "/*nr_other=\"x\"*/SELECT 1", "/*nr_other=?*/SELECT ?")]
    [TestCase(DatastoreVendor.Postgres, "/*nr_other=\"x\"*/SELECT 1", "?SELECT ?")]
    public void GetObfuscatedSql_DoesNotKeepLookAlikeComments(DatastoreVendor vendor, string sql, string expected)
    {
        Assert.That(obfuscator.GetObfuscatedSql(sql, vendor), Is.EqualTo(expected));
    }

    [TestCase(DatastoreVendor.Other, "\"dude\" and dob = '10/31/1955' -- c", "? and dob = ? -- c")]
    [TestCase(DatastoreVendor.MySQL, "\"dude\" and dob = '10/31/1955' -- c", "? and dob = ? -- c")]
    [TestCase(DatastoreVendor.Oracle, "\"dude\" and dob = '10/31/1955' -- c", "? and dob = ? -- c")]
    public void GetObfuscatedSql_NonPostgres_KeepsLegacyBehavior(DatastoreVendor vendor, string sql, string expected)
    {
        Assert.That(obfuscator.GetObfuscatedSql(sql, vendor), Is.EqualTo(expected));
    }

}