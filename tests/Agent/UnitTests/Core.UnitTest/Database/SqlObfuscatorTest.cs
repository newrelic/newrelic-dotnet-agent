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
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122 and True"), Is.EqualTo("Select * from users where ssn = ? and ?")); // spec rule: booleans
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_id_with_questionmark_having_multi_parameters_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where ssn = 433871122 and True", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where ssn = ? and True"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_decimal_no_remainder_with_questionmark_having_multi_parameters()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3. and True"), Is.EqualTo("Select * from users where number = ? and ?")); // spec rule: booleans
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_decimal_no_remainder_with_questionmark_having_multi_parameters_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3. and True", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where number = ? and True"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_decimal_with_questionmark_having_multi_parameters()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3.14159 and True"), Is.EqualTo("Select * from users where number = ? and ?")); // spec rule: booleans
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_decimal_with_questionmark_having_multi_parameters_when_datastore_vendor_is_MSSQL()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3.14159 and True", DatastoreVendor.MSSQL), Is.EqualTo("Select * from users where number = ? and True"));
    }

    [Test]
    public void verify_GetObfuscatedSql_replaces_string_with_int_and_ticks_with_questionmark_having_multi_parameters()
    {
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where number = 3.14food'' and True"), Is.EqualTo("Select * from users where number = ?? and ?")); // spec rules: booleans; a numeric literal swallows trailing identifier chars (3.14food -> ?), then '' -> ?

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
        Assert.That(obfuscator.GetObfuscatedSql("Select * from users where name = 'dude''fude'\"bube\""), Is.EqualTo("Select * from users where name = ??")); // spec rule: string_with_twin_single_quotes

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
        Assert.That(obfuscator.GetObfuscatedSql(stim), Is.EqualTo("?")); // spec rule: malformed/unterminated_single_quoted_string
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
    [TestCase(DatastoreVendor.Other)]
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

    [TestCase(DatastoreVendor.Postgres)]
    [TestCase(DatastoreVendor.MySQL)]
    public void GetObfuscatedSql_MasksBodyWhenUnterminatedPastTheLimit(DatastoreVendor vendor)
    {
        var body = "SELECT " + new string('a', 20000) + " WHERE b = 'x";
        Assert.That(obfuscator.GetObfuscatedSql(body, vendor), Is.EqualTo("?"));
        Assert.That(obfuscator.GetObfuscatedSql(MetadataComment + body, vendor), Is.EqualTo(MetadataComment + "?"));
    }

    [TestCase(DatastoreVendor.Postgres)]
    [TestCase(DatastoreVendor.MySQL)]
    public void GetObfuscatedSql_TruncatesAtTheLimit(DatastoreVendor vendor)
    {
        var sql = "SELECT " + string.Concat(Enumerable.Repeat("1, ", 10000));
        var expected = ("SELECT " + string.Concat(Enumerable.Repeat("?, ", 10000))).Substring(0, 16384);
        Assert.That(obfuscator.GetObfuscatedSql(sql, vendor), Is.EqualTo(expected));
    }

    [TestCase(DatastoreVendor.Other, "/*nr_service_guid=\"a\"b\"*/SELECT 1", "?SELECT ?")]
    [TestCase(DatastoreVendor.Other, "/*nr_other=\"x\"*/SELECT 1", "?SELECT ?")]
    [TestCase(DatastoreVendor.Postgres, "/*nr_other=\"x\"*/SELECT 1", "?SELECT ?")]
    public void GetObfuscatedSql_DoesNotKeepLookAlikeComments(DatastoreVendor vendor, string sql, string expected)
    {
        Assert.That(obfuscator.GetObfuscatedSql(sql, vendor), Is.EqualTo(expected));
    }

    [TestCase(DatastoreVendor.Other, "\"dude\" and dob = '10/31/1955' -- c", "? and dob = ? ?")]
    [TestCase(DatastoreVendor.MySQL, "SELECT `c` FROM t WHERE a = \"b\" # c", "SELECT `c` FROM t WHERE a = ? ?")]
    [TestCase(DatastoreVendor.Oracle, "SELECT \"C\" FROM t WHERE a = q'[b]'", "SELECT \"C\" FROM t WHERE a = ?")]
    [TestCase(DatastoreVendor.MSSQL, "SELECT * FROM #t WHERE a = 'b' -- c", "SELECT * FROM #t WHERE a = ? ?")]
    public void GetObfuscatedSql_UsesVendorDialect(DatastoreVendor vendor, string sql, string expected)
    {
        Assert.That(obfuscator.GetObfuscatedSql(sql, vendor), Is.EqualTo(expected));
    }

    // SHOWPLAN_ALL child rows start with a tree prefix ("  |--", "       |    |--") that is kept verbatim; the -- in it is not a comment.
    [TestCase("  |--Clustered Index Scan(OBJECT:([NewRelic].[dbo].[TeamMembers].[PK]), WHERE:([FirstName]=N'John'))",
              "  |--Clustered Index Scan(OBJECT:([NewRelic].[dbo].[TeamMembers].[PK]), WHERE:([FirstName]=N?))")]
    [TestCase("       |    |--Compute Scalar(DEFINE:([Expr1]=(1)))", "       |    |--Compute Scalar(DEFINE:([Expr1]=(?)))")]
    [TestCase("\t|--Top(TOP EXPRESSION:((10)))", "\t|--Top(TOP EXPRESSION:((?)))")]
    [TestCase("  |--Filter(WHERE:([a]='x')) -- tail", "  |--Filter(WHERE:([a]=?)) ?")]
    [TestCase("|--", "|--")]
    // Root rows have no prefix and are obfuscated as ordinary SQL
    [TestCase("SELECT * FROM t WHERE a = 'b' -- c", "SELECT * FROM t WHERE a = ? ?")]
    [TestCase("--x", "?")]
    [TestCase("  --x", "  ?")]
    [TestCase("  |-x", "  |-x")]
    [TestCase("  |", "  |")]
    [TestCase("|-", "|-")]
    [TestCase("", "")]
    public void GetObfuscatedExplainPlanValue_MsSql_KeepsShowPlanTreePrefix(string value, string expected)
    {
        Assert.That(obfuscator.GetObfuscatedExplainPlanValue(value, DatastoreVendor.MSSQL), Is.EqualTo(expected));
    }

    [Test]
    public void GetObfuscatedExplainPlanValue_Null_ReturnsNull()
    {
        Assert.That(obfuscator.GetObfuscatedExplainPlanValue(null, DatastoreVendor.MSSQL), Is.Null);
    }

    [TestCase("  |--Clustered Index Scan(WHERE:([FirstName]=N'John'))")]
    [TestCase("SELECT * FROM t WHERE a = 'b' -- c")]
    public void GetObfuscatedExplainPlanValue_RawObfuscator_ReturnsValueUnchanged(string value)
    {
        var raw = SqlObfuscator.GetSqlObfuscator("raw");
        Assert.That(raw.GetObfuscatedExplainPlanValue(value, DatastoreVendor.MSSQL), Is.EqualTo(value));
    }

    [TestCase("  |--Clustered Index Scan(WHERE:([FirstName]=N'John'))")]
    [TestCase("SELECT * FROM t WHERE a = 'b'")]
    public void GetObfuscatedExplainPlanValue_OffObfuscator_ReturnsNull(string value)
    {
        var off = SqlObfuscator.GetSqlObfuscator("off");
        Assert.That(off.GetObfuscatedExplainPlanValue(value, DatastoreVendor.MSSQL), Is.Null);
    }

}