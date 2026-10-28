// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Database;

/// <summary>
/// Literal-masking rules that differ between SQL dialects. See the SQL-Obfuscation-LEGACY agent spec.
/// </summary>
[Flags]
public enum SqlDialectFeatures
{
    None = 0,

    /// <summary>"x" is a quoted identifier and is kept verbatim. Without this flag it is a string literal.</summary>
    DoubleQuotedIdentifiers = 1 << 0,

    /// <summary>$tag$...$tag$ string bodies and $1 positional parameters. $ is also legal inside identifiers.</summary>
    DollarQuotedStrings = 1 << 1,

    /// <summary># starts an end-of-line comment.</summary>
    HashComments = 1 << 2,

    /// <summary>
    /// A backslash before a quote may or may not be an escape (it depends on server settings), so the rest of the
    /// query is obfuscated rather than risk leaking a value.
    /// </summary>
    BackslashEscapes = 1 << 3,

    /// <summary>Unquoted UUIDs: 32 hex digits, optionally dash-separated and brace-wrapped.</summary>
    UuidLiterals = 1 << 4,

    /// <summary>true, false and null are literals.</summary>
    BooleanLiterals = 1 << 5,

    /// <summary>
    /// # starts text that is masked through the end of the query. Postgres has no # comment; it is an operator (XOR,
    /// JSONB #>, #>>, #-), so masking only to end of line could fall out of step with string literals.
    /// </summary>
    HashMasksRestOfQuery = 1 << 6,

    /// <summary>`x` is a quoted identifier and is kept verbatim.</summary>
    BacktickIdentifiers = 1 << 7,

    /// <summary>
    /// Oracle alternative quoting: [n]q'&lt;d&gt;...&lt;d&gt;' where the delimiter d is any character that is not whitespace and
    /// not '. The brackets [ { &lt; ( pair with ] } &gt; ); any other d closes with itself. The n/N (national character set)
    /// prefix is optional.
    /// </summary>
    OracleQuotedStrings = 1 << 8,

    /// <summary># and $ are legal inside identifiers (Oracle), so only a standalone # starts a comment.</summary>
    HashInIdentifiers = 1 << 9,

    /// <summary>[x] is a quoted identifier and is kept verbatim; ]] escapes ].</summary>
    BracketIdentifiers = 1 << 10,

    /// <summary>
    /// # followed by whitespace or the end of the query masks the rest of the query; any other # is ordinary text, so
    /// #temp and ##global table names are kept. For an unknown dialect, where such a # may be a MySQL or Oracle comment:
    /// masking to the end of the query rather than the line keeps a quote in the comment from desyncing the scan.
    /// </summary>
    HashWhitespaceMasksRestOfQuery = 1 << 11,

    /// <summary>
    /// -- starts an end-of-line comment only when followed by an ASCII space or control character, or the end of the query
    /// (MySQL). Any other --, including one followed by a Unicode space such as NBSP,
    /// is read by the server as SQL (5--1 is arithmetic), so the rest of the query is masked rather than risk a quote in
    /// it desyncing the scan.
    /// </summary>
    DashDashRequiresWhitespace = 1 << 12,

    /// <summary>
    /// A dollar-quote opener ($$ or $tag$) masks the rest of the query. For an unknown dialect, where it may open a
    /// Postgres, Redshift or Snowflake string literal. Any other $ is ordinary text, so $name and $1 parameters are kept.
    /// </summary>
    DollarQuoteMasksRestOfQuery = 1 << 13,
}

public static class SqlDialects
{
    public const SqlDialectFeatures Postgres =
        SqlDialectFeatures.DoubleQuotedIdentifiers |
        SqlDialectFeatures.DollarQuotedStrings |
        SqlDialectFeatures.HashMasksRestOfQuery |
        SqlDialectFeatures.BackslashEscapes |
        SqlDialectFeatures.UuidLiterals |
        SqlDialectFeatures.BooleanLiterals;

    public const SqlDialectFeatures MySql =
        SqlDialectFeatures.BacktickIdentifiers |
        SqlDialectFeatures.HashComments |
        SqlDialectFeatures.BackslashEscapes |
        SqlDialectFeatures.BooleanLiterals |
        SqlDialectFeatures.DashDashRequiresWhitespace;

    public const SqlDialectFeatures Oracle =
        SqlDialectFeatures.DoubleQuotedIdentifiers |
        SqlDialectFeatures.OracleQuotedStrings |
        SqlDialectFeatures.HashComments |
        SqlDialectFeatures.HashInIdentifiers |
        SqlDialectFeatures.BackslashEscapes;

    // SQL Server: [x] is a quoted identifier, backslash is never an escape and # prefixes temp tables. Not covered by the
    // spec's dialect list; the cross-agent mssql cases define it. 0x binary literals are already masked whole by the
    // numeric-literal rule.
    public const SqlDialectFeatures MsSql = SqlDialectFeatures.BracketIdentifiers;

    // Every other vendor: the dialect is unknown, so mask everything any dialect could treat as a literal. # is never an
    // end-of-line comment, because that would swallow SQL Server temp-table names (#t, ##g) reached via ODBC/OLE DB/Other;
    // those are kept as ordinary text. A # followed by whitespace or the end of the query cannot be a temp table and may be
    // a MySQL or Oracle comment, so it masks the rest of the query. A $$ or $tag$ may open a dollar-quoted string, so it
    // masks the rest of the query too; full dollar quoting is not used because a $name parameter (Couchbase N1QL) would
    // then read as an unterminated tag and mask the whole statement.
    public const SqlDialectFeatures Default =
        SqlDialectFeatures.BacktickIdentifiers |
        SqlDialectFeatures.BackslashEscapes |
        SqlDialectFeatures.UuidLiterals |
        SqlDialectFeatures.BooleanLiterals |
        SqlDialectFeatures.HashWhitespaceMasksRestOfQuery |
        SqlDialectFeatures.DollarQuoteMasksRestOfQuery;

    public static SqlDialectFeatures ForVendor(DatastoreVendor vendor)
    {
        switch (vendor)
        {
            case DatastoreVendor.Postgres:
                return Postgres;
            case DatastoreVendor.MySQL:
                return MySql;
            case DatastoreVendor.Oracle:
                return Oracle;
            case DatastoreVendor.MSSQL:
                return MsSql;
            default:
                return Default;
        }
    }
}
