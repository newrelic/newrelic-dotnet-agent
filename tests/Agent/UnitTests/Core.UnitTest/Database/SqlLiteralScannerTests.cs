// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NUnit.Framework;

// Same namespace as the sibling SqlObfuscatorTest.cs, so the Core types need no using (an unused using fails the build).
namespace NewRelic.Agent.Core.Database;

[TestFixture]
public class SqlLiteralScannerTests
{
    private const SqlDialectFeatures None = SqlDialectFeatures.None;
    private const SqlDialectFeatures Pg = SqlDialects.Postgres;

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

    [Test]
    public void Obfuscate_LargeInput_IsMaskedCompletely()
    {
        const int rows = 200000;
        var sql = "INSERT INTO t VALUES " + string.Join(",", Enumerable.Repeat("(1,'a',\"c\")", rows));
        var expected = "INSERT INTO t VALUES " + string.Join(",", Enumerable.Repeat("(?,?,\"c\")", rows));

        Assert.That(SqlLiteralScanner.Obfuscate(sql, Pg), Is.EqualTo(expected));
    }
}
