// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

// Same namespace as the sibling SqlObfuscatorTest.cs, so the Core types need no using (an unused using fails the build).
namespace NewRelic.Agent.Core.Database;

[TestFixture]
public class SqlLiteralScannerTests
{
    private const SqlDialectFeatures None = SqlDialectFeatures.None;
    private const SqlDialectFeatures Pg = SqlDialects.Postgres;
    private const SqlDialectFeatures MySql = SqlDialects.MySql;
    private const SqlDialectFeatures Ora = SqlDialects.Oracle;
    private const SqlDialectFeatures Ms = SqlDialects.MsSql;
    private const SqlDialectFeatures Def = SqlDialects.Default;

    [Test]
    public void Obfuscate_Null_ReturnsNull()
    {
        Assert.That(SqlLiteralScanner.Obfuscate(null, Pg), Is.Null);
    }

    // Comments
    [TestCase("SELECT 1 -- note", None, "SELECT ? ?")]
    [TestCase("a -- c\r\nb", None, "a ?\r\nb")]
    [TestCase("a -- c\nb", None, "a ?\nb")]
    [TestCase("a -- c\rb", None, "a ?\rb")]
    // Postgres has no # comment (it is XOR / JSONB #>, #>>, #-), so # masks the rest of the query: a string opened after it could span lines.
    [TestCase("a # c\nb", Pg, "a ?")]
    [TestCase("a # c\nb", SqlDialectFeatures.HashComments, "a ?\nb")]
    [TestCase("a # c", None, "a # c")]
    [TestCase("data #>> '{a}' FROM t WHERE id = 5", Pg, "data ?")]
    [TestCase("SELECT doc #>> '{a}', 'line one\nline two' FROM t -- don't", Pg, "SELECT doc ?")]
    [TestCase("x = 1 AND data #>> '{k}' = 'some\nvalue' AND name = 'bob' -- it's done", Pg, "x = ? AND data ?")]
    [TestCase("a /* c */ b", None, "a ? b")]
    // The scanner masks every comment. Keeping the agent's metadata comment is SqlObfuscator's job (Task 3), not the scanner's.
    [TestCase("/*nr_service_guid=\"abc\"*/SELECT 1", Pg, "?SELECT ?")]
    [TestCase("a /* never closed", None, "?")]
    [TestCase("a */ b", None, "?")]
    [TestCase("/* /* */ secret", Pg, "?")]
    [TestCase("/* a /* b */ c */ d", Pg, "?")]
    // Single-quoted strings
    [TestCase("x = ''", None, "x = ?")]
    [TestCase("x = 'it''s'", None, "x = ?")]
    [TestCase("x = 'a\\'b' AND y = 1", Pg, "x = ?")]
    [TestCase("x = 'a\\' AND y = 1", None, "x = ? AND y = ?")]
    [TestCase("x = 'a\\b' AND y = 1", Pg, "x = ? AND y = ?")]
    [TestCase("x = 'never closed", Pg, "?")]
    [TestCase("x = E'a\\'b'", Pg, "x = E?")]
    [TestCase("x = E'a\\\\' AND y = 1", Pg, "x = E?")]
    [TestCase("x = U&'d\\0061t' UESCAPE '!'", Pg, "x = U&? UESCAPE ?")]
    [TestCase("x = B'101' AND y = X'1F'", Pg, "x = B? AND y = X?")]
    // Double quotes
    [TestCase("\"t\".\"c\" = 'v'", Pg, "\"t\".\"c\" = ?")]
    [TestCase("\"t001\" = 1", Pg, "\"t001\" = ?")]
    [TestCase("\"a\"\"b\" = 1", Pg, "\"a\"\"b\" = ?")]
    [TestCase("\"a\\\" = 1", Pg, "\"a\\\" = ?")]
    [TestCase("\"never closed = 'secret'", Pg, "?")]
    [TestCase("\"t\".\"c\" = 'v'", None, "?.? = ?")]
    [TestCase("x = \"a\\\"b\"", SqlDialectFeatures.BackslashEscapes, "x = ?")]
    // Dollar quoting and positional parameters
    [TestCase("x = $$body$$ AND y", Pg, "x = ? AND y")]
    [TestCase("x = $a$ $b$in$b$ $a$ AND y", Pg, "x = ? AND y")]
    [TestCase("x = $tag$ never closed", Pg, "x = ?")]
    [TestCase("VALUES ($1, $22)", Pg, "VALUES ($?, $?)")]
    [TestCase("a$b = 1", Pg, "a$b = ?")]
    [TestCase("x = $ 1", Pg, "?")]
    [TestCase("x = $a b", Pg, "?")]
    [TestCase("x = $", Pg, "?")]
    [TestCase("x = $a", Pg, "?")]
    [TestCase("x = $$body$$", None, "x = $$body$$")]
    [TestCase("a$$x$$ = 1", Pg, "a$$x$$ = ?")]
    // UUIDs
    [TestCase("x=01234567-89ab-cdef-0123-456789abcdef", Pg, "x=?")]
    [TestCase("x={01234567-89ab-cdef-0123-456789abcdef}", Pg, "x=?")]
    [TestCase("x=0123456789abcdef0123456789abcdef", Pg, "x=?")]
    [TestCase("x=abcdef01-2345-6789-abcd-ef0123456789", Pg, "x=?")]
    [TestCase("x={012-345678-9abc-def012345678-9abcdef}", Pg, "x=?")]
    [TestCase("x=0123456789abcdef0123456789abcdefz", Pg, "x=?")]
    [TestCase("x={abc}", Pg, "x={abc}")]
    [TestCase("x=01234567-89ab-cdef-0123-456789abcdef", None, "x=?-?-cdef-?-?")]
    // A UUID never runs through a -- comment: the comment hides the second half of the digits.
    [TestCase("x = 1234567890123456--1234567890123456 don't\nAND name = 'bob' -- ok'", Pg, "x = ??\nAND name = ? ?")]
    [TestCase("x = 12-", Pg, "x = ?-")]
    // Prefixed and suffixed numeric literals
    [TestCase("x = 0x1F", Pg, "x = ?")]
    [TestCase("x = 0X1f", Pg, "x = ?")]
    [TestCase("x = 0x", Pg, "x = ?")]
    [TestCase("x = 0x1F", None, "x = ?")]
    [TestCase("x = 1_000_000", Pg, "x = ?")]
    [TestCase("x = 0b1010", Pg, "x = ?")]
    [TestCase("x = 0o777", Pg, "x = ?")]
    [TestCase("x = 0xFF_FF", Pg, "x = ?")]
    // Booleans and null
    [TestCase("a = true AND b = FALSE AND c IS Null", Pg, "a = ? AND b = ? AND c IS ?")]
    [TestCase("truestory = nulls", Pg, "truestory = nulls")]
    [TestCase("a = true", None, "a = true")]
    // Numbers
    [TestCase("x = 1.234e-5", Pg, "x = ?")]
    [TestCase("x = 1E+5", Pg, "x = ?")]
    [TestCase("x = 1e5", Pg, "x = ?")]
    [TestCase("x = 1e", Pg, "x = ?")]
    [TestCase("x = 1e+", Pg, "x = ?+")]
    [TestCase("x = .5", Pg, "x = ?")]
    [TestCase("x=-5", Pg, "x=?")]
    [TestCase("-5", Pg, "?")]
    [TestCase("IN (-1, -2.5)", Pg, "IN (?, ?)")]
    [TestCase("col-1", Pg, "col-?")]
    [TestCase("f(x) -1", Pg, "f(x) -?")]
    [TestCase("? - 1", Pg, "? - ?")]
    [TestCase("a - -1", Pg, "a - ?")]
    [TestCase("x = 1.", Pg, "x = ?")]
    [TestCase("x = .", Pg, "x = .")]
    [TestCase("a -", Pg, "a -")]
    // Identifiers and passthrough
    [TestCase("SELECT t001.c2, _x1 FROM t", Pg, "SELECT t001.c2, _x1 FROM t")]
    [TestCase("a <> b;", Pg, "a <> b;")]
    [TestCase("", Pg, "")]
    public void Obfuscate(string sql, SqlDialectFeatures features, string expected)
    {
        Assert.That(SqlLiteralScanner.Obfuscate(sql, features), Is.EqualTo(expected));
    }

    // Backtick identifiers
    [TestCase("SELECT `t`.`c` FROM `t` WHERE `c` = 'v'", MySql, "SELECT `t`.`c` FROM `t` WHERE `c` = ?")]
    [TestCase("`a``b` = 1", MySql, "`a``b` = ?")]
    [TestCase("`never closed = 'secret'", MySql, "?")]
    [TestCase("`t` = 1", Ms, "`t` = ?")]
    // MySQL double quotes are strings; # is a comment
    [TestCase("c = \"v\" # tail", MySql, "c = ? ?")]
    [TestCase("x = 0xFF AND y = TRUE", MySql, "x = ? AND y = ?")]
    // MySQL: -- starts a comment only when followed by whitespace; otherwise the rest of the query is masked, because the
    // server would read the text after it as SQL and a quote in it could desync the scan.
    [TestCase("x = 1 --'\nsecret' -- '\n", MySql, "x = ? ?")]
    [TestCase("x = 1 -- c\ny = 2", MySql, "x = ? ?\ny = ?")]
    [TestCase("x = 1 --\tc\ny = 2", MySql, "x = ? ?\ny = ?")]
    [TestCase("x = 1 --", MySql, "x = ? ?")]
    // Only ASCII space and control characters end MySQL's -- marker; a Unicode space such as NBSP does not.
    [TestCase("SELECT 1 -- '\nsecret \"'\" # \"", MySql, "SELECT ? ?")]
    [TestCase("x = 1 --\u0001c\ny = 2", MySql, "x = ? ?\ny = ?")]
    [TestCase("x = 5--1 AND y = 'a'", MySql, "x = ??")]
    [TestCase("x = 1 --'\ny = 2", Ms, "x = ? ?\ny = ?")]
    // Oracle q-quotes
    [TestCase("x = q'[it's]' AND y = 1", Ora, "x = ? AND y = ?")]
    [TestCase("x = Q'{a}' AND y = 1", Ora, "x = ? AND y = ?")]
    [TestCase("x = q'<a>' AND y = 1", Ora, "x = ? AND y = ?")]
    [TestCase("x = q'(a)' AND y = 1", Ora, "x = ? AND y = ?")]
    [TestCase("x = q'[never closed", Ora, "x = ?")]
    [TestCase("x = q'!a!'", Ora, "x = ?")]
    [TestCase("x = q'!O'Brien's!' AND y = 1", Ora, "x = ? AND y = ?")]
    [TestCase("x = nq'[O'Brien's]' AND y = 1", Ora, "x = ? AND y = ?")]
    [TestCase("x = Nq'{a}'", Ora, "x = ?")]
    [TestCase("x = q'", Ora, "?")]
    [TestCase("x = q' a'", Ora, "x = q?")]
    [TestCase("x = q'' AND y = 1", Ora, "x = q? AND y = ?")]
    [TestCase("SELECT quantity, nq FROM t", Ora, "SELECT quantity, nq FROM t")]
    [TestCase("SELECT name FROM t", Ora, "SELECT name FROM t")]
    [TestCase("x = q'[a]b]' AND y = 1", Ora, "x = ? AND y = ?")]
    [TestCase("x = q'[a]", Ora, "x = ?")]
    [TestCase("x = q'!a!b!' AND y = 1", Ora, "x = ? AND y = ?")]
    [TestCase("x = q'[a]'", MySql, "x = q?")]
    // Oracle identifiers may contain # and $; standalone # is a comment
    [TestCase("SELECT EMP#, V$SESSION FROM t # tail", Ora, "SELECT EMP#, V$SESSION FROM t ?")]
    // An identifier ending in # is an operand, so a following minus is binary and kept.
    [TestCase("SELECT EMP#-1 FROM t", Ora, "SELECT EMP#-? FROM t")]
    [TestCase("SELECT EMP#-1 FROM t", None, "SELECT EMP#? FROM t")]
    [TestCase("SELECT \"T\".\"C\" FROM \"T\" WHERE ROWNUM <= 1", Ora, "SELECT \"T\".\"C\" FROM \"T\" WHERE ROWNUM <= ?")]
    [TestCase("a = true", Ora, "a = true")]
    // MSSQL: no # comments, no backslash escapes, double quotes are strings, hex binary literals
    [TestCase("SELECT * FROM #t JOIN ##g ON 1 = 1 WHERE id = 5", Ms, "SELECT * FROM #t JOIN ##g ON ? = ? WHERE id = ?")]
    [TestCase("x = 'foo\\''bar' AND y = 1", Ms, "x = ? AND y = ?")]
    [TestCase("x = \"Quoty O\"\"Quoterson\"", Ms, "x = ?")]
    [TestCase("x = 0xDEADBEEF", Ms, "x = ?")]
    [TestCase("x = 1 and True -- c", Ms, "x = ? and True ?")]
    [TestCase("SELECT [it's] FROM t WHERE a = 1", Ms, "SELECT [it's] FROM t WHERE a = ?")]
    [TestCase("[it's] = 'secret' AND y = [o's]", Ms, "[it's] = ? AND y = [o's]")]
    [TestCase("[a]]b] = 1", Ms, "[a]]b] = ?")]
    [TestCase("x = [never", Ms, "?")]
    // Default: conservative
    [TestCase("\"dude\" = 'x' AND `t` = TRUE AND #t = 1", Def, "? = ? AND `t` = ? AND #t = ?")]
    [TestCase("x = 01234567-89ab-cdef-0123-456789abcdef", Def, "x = ?")]
    // Default: # followed by whitespace or end of input masks the rest of the query; #temp and ##global are ordinary text.
    [TestCase("SELECT * FROM t # don't\nWHERE pw = 'secret' # it's\n", Def, "SELECT * FROM t ?")]
    [TestCase("SELECT * FROM #t WHERE id = 5", Def, "SELECT * FROM #t WHERE id = ?")]
    [TestCase("SELECT * FROM ##g WHERE id = 5", Def, "SELECT * FROM ##g WHERE id = ?")]
    [TestCase("x = 1 #", Def, "x = ? ?")]
    [TestCase("x = 1 #\tit's", Def, "x = ? ?")]
    [TestCase("SELECT * FROM #t # x = 1", Ms, "SELECT * FROM #t # x = ?")]
    public void Obfuscate_Dialects(string sql, SqlDialectFeatures features, string expected)
    {
        Assert.That(SqlLiteralScanner.Obfuscate(sql, features), Is.EqualTo(expected));
    }

    [TestCase(DatastoreVendor.Postgres, SqlDialects.Postgres)]
    [TestCase(DatastoreVendor.MySQL, SqlDialects.MySql)]
    [TestCase(DatastoreVendor.Oracle, SqlDialects.Oracle)]
    [TestCase(DatastoreVendor.MSSQL, SqlDialects.MsSql)]
    [TestCase(DatastoreVendor.IBMDB2, SqlDialects.Default)]
    [TestCase(DatastoreVendor.ODBC, SqlDialects.Default)]
    [TestCase(DatastoreVendor.Other, SqlDialects.Default)]
    [TestCase(DatastoreVendor.MongoDB, SqlDialects.Default)]
    public void ForVendor(DatastoreVendor vendor, SqlDialectFeatures expected)
    {
        Assert.That(SqlDialects.ForVendor(vendor), Is.EqualTo(expected));
    }

    [Test]
    public void MsSqlAndDefault_NeverTreatHashAsComment()
    {
        const SqlDialectFeatures hashFeatures = SqlDialectFeatures.HashComments | SqlDialectFeatures.HashMasksRestOfQuery;

        Assert.Multiple(() =>
        {
            Assert.That(SqlDialects.MsSql & hashFeatures, Is.EqualTo(SqlDialectFeatures.None));
            Assert.That(SqlDialects.Default & hashFeatures, Is.EqualTo(SqlDialectFeatures.None));
            Assert.That(SqlDialects.MsSql & SqlDialectFeatures.HashWhitespaceMasksRestOfQuery, Is.EqualTo(SqlDialectFeatures.None));
        });
    }

    [Test]
    public void Obfuscate_WithPrefix_Null_ReturnsNull()
    {
        Assert.That(SqlLiteralScanner.Obfuscate(null, 0, "/*p*/", 10, Ms), Is.Null);
    }

    // The overload must equal prefix + Obfuscate(rest), truncated to maxLength, for every input.
    [TestCase("/*p*/SELECT * FROM t WHERE a = 'b' AND c = 12", 5, "/*p*/", 1000, Ms)]
    [TestCase("/*p*/SELECT * FROM t WHERE a = 'b' AND c = 12", 5, "/*p*/", 20, Ms)]
    [TestCase("/*p*/SELECT * FROM t WHERE a = 'b' AND c = 12", 5, "/*p*/", 3, Ms)]
    [TestCase("/*p*/SELECT * FROM t WHERE a = 'b' AND c = 12", 5, null, 12, Ms)]
    [TestCase("/*p*/SELECT * FROM t WHERE a = 'b' AND c = 12", 0, null, 1000, Ms)]
    // Unterminated input still masks everything after the prefix, however short maxLength is
    [TestCase("/*p*/SELECT 'x", 5, "/*p*/", 1000, Pg)]
    [TestCase("/*p*/SELECT 'x", 5, "/*p*/", 3, Pg)]
    [TestCase("/*p*/SELECT 'x", 5, null, 1000, Pg)]
    // The unterminated part lies beyond maxLength: the scan still runs to the end of the input
    [TestCase("/*p*/SELECT a, b, c FROM t WHERE x = 'never closed", 5, "/*p*/", 10, Ms)]
    // A leading minus is unary whatever the prefix ends with
    [TestCase("xx-5 AND y = 1", 2, "/*p*/", 1000, Ms)]
    [TestCase("xx-5 AND y = 1", 2, "abc", 1000, Ms)]
    [TestCase("xx   -5", 2, "abc", 1000, Ms)]
    [TestCase("xx", 2, "/*p*/", 1000, Ms)]
    [TestCase("/*p*/a # c\nb", 5, "/*p*/", 1000, Pg)]
    public void Obfuscate_WithStartIndexPrefixAndMaxLength_EqualsPrefixedAndTruncatedOutput(string sql, int startIndex, string prefix, int maxLength, SqlDialectFeatures features)
    {
        var full = prefix + SqlLiteralScanner.Obfuscate(sql.Substring(startIndex), features);
        var expected = full.Substring(0, Math.Min(full.Length, maxLength));

        Assert.That(SqlLiteralScanner.Obfuscate(sql, startIndex, prefix, maxLength, features), Is.EqualTo(expected));
    }

    [Test]
    public void Obfuscate_LargeInput_IsMaskedCompletely()
    {
        const int rows = 200000;
        var sql = "INSERT INTO t VALUES " + string.Join(",", Enumerable.Repeat("(1,'a',\"c\")", rows));
        var expected = "INSERT INTO t VALUES " + string.Join(",", Enumerable.Repeat("(?,?,\"c\")", rows));

        Assert.That(SqlLiteralScanner.Obfuscate(sql, Pg), Is.EqualTo(expected));
    }

    // Prefix and start index: the prefix is written verbatim and the SQL before startIndex is skipped.
    [TestCase("XXSELECT 'a'", 2, "P:", 100, "P:SELECT ?")]
    // The prefix never makes a following minus binary.
    [TestCase("-1", 0, "x", 100, "x?")]
    // Output is capped at maxLength, including part of an identifier or quoted identifier.
    [TestCase("SELECT 1, 2, 3", 0, null, 8, "SELECT ?")]
    [TestCase("SELECT abcdef", 0, null, 9, "SELECT ab")]
    [TestCase("ab cd", 0, null, 2, "ab")]
    [TestCase("\"abcdef\"", 0, null, 3, "\"ab")]
    // Input that cannot be tokenized safely past the cap still masks the whole body.
    [TestCase("SELECT 1, 'x", 0, "P", 4, "P?")]
    // A prefix longer than maxLength is truncated too.
    [TestCase("'x", 0, "PREFIX", 3, "PRE")]
    [TestCase("a", 0, "PREFIX", 3, "PRE")]
    public void Obfuscate_WithPrefixStartIndexAndMaxLength(string sql, int startIndex, string prefix, int maxLength, string expected)
    {
        Assert.That(SqlLiteralScanner.Obfuscate(sql, startIndex, prefix, maxLength, Pg), Is.EqualTo(expected));
    }

    [Test]
    public void Obfuscate_WithPrefixStartIndexAndMaxLength_Null_ReturnsNull()
    {
        Assert.That(SqlLiteralScanner.Obfuscate(null, 0, "P", 10, Pg), Is.Null);
    }

    // Out-of-range arguments mask everything, including the prefix, instead of throwing.
    [TestCase(-1, 10)]
    [TestCase(4, 10)]
    [TestCase(0, -1)]
    public void Obfuscate_WithPrefixStartIndexAndMaxLength_InvalidArguments_ReturnFullyObfuscated(int startIndex, int maxLength)
    {
        Assert.That(SqlLiteralScanner.Obfuscate("abc", startIndex, "P", maxLength, Pg), Is.EqualTo("?"));
    }

    // Boundaries: startIndex at the end of sql and a zero maxLength are valid.
    [TestCase(3, 10, "P")]
    [TestCase(0, 0, "")]
    public void Obfuscate_WithPrefixStartIndexAndMaxLength_BoundaryArguments(int startIndex, int maxLength, string expected)
    {
        Assert.That(SqlLiteralScanner.Obfuscate("abc", startIndex, "P", maxLength, Pg), Is.EqualTo(expected));
    }
}
