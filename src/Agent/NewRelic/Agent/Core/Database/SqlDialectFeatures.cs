// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

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
}
