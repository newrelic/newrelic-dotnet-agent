// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;

namespace NewRelic.Agent.Core.Database;

/// <summary>
/// Replaces SQL literals (strings, numbers, comments and dialect-specific literals) with "?" in a single linear pass.
/// Input that cannot be tokenized safely (an unterminated string, comment or quoted identifier, or a stray comment
/// terminator) is replaced entirely with "?", because over-obfuscating is always preferable to leaking a value.
/// Does not use regular expressions: the input is not length-limited and must never backtrack.
/// </summary>
public static class SqlLiteralScanner
{
    private const string FullyObfuscated = "?";
    private const int Unterminated = -1;
    private const int UuidHexDigitCount = 32;

    public static string Obfuscate(string sql, SqlDialectFeatures features)
    {
        if (sql == null)
        {
            return null;
        }

        var sb = new StringBuilder(sql.Length);
        var length = sql.Length;
        var i = 0;
        while (i < length)
        {
            var ch = sql[i];
            var next = i + 1 < length ? sql[i + 1] : '\0';

            // Postgres has no # comment, so a # masks the rest of the query: a string opened after it could span lines and desync the scan.
            if (ch == '#' && Has(features, SqlDialectFeatures.HashMasksRestOfQuery))
            {
                sb.Append('?');
                i = length;
                continue;
            }

            // End-of-line comments
            if ((ch == '-' && next == '-') || (ch == '#' && Has(features, SqlDialectFeatures.HashComments)))
            {
                sb.Append('?');
                i = EndOfLine(sql, i);
                continue;
            }

            // Block comments
            if (ch == '/' && next == '*')
            {
                var close = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    return FullyObfuscated;
                }

                // Postgres block comments nest, so a second opener before the first closer makes the extent ambiguous: mask the rest.
                var nested = sql.IndexOf("/*", i + 2, close - i - 1, StringComparison.Ordinal);
                sb.Append('?');
                i = nested >= 0 ? length : close + 2;
                continue;
            }

            if (ch == '*' && next == '/')
            {
                return FullyObfuscated;
            }

            // String literals
            if (ch == '\'' || (ch == '"' && !Has(features, SqlDialectFeatures.DoubleQuotedIdentifiers)))
            {
                i = SkipQuoted(sql, i, Has(features, SqlDialectFeatures.BackslashEscapes));
                if (i == Unterminated)
                {
                    return FullyObfuscated;
                }

                sb.Append('?');
                continue;
            }

            // Quoted identifiers are kept verbatim
            if (ch == '"')
            {
                var end = SkipQuoted(sql, i, false);
                if (end == Unterminated)
                {
                    return FullyObfuscated;
                }

                sb.Append(sql, i, end - i);
                i = end;
                continue;
            }

            // Dollar-quoted strings. A $ followed by a digit is a positional parameter: the $ passes through and the digits become ?.
            if (ch == '$' && Has(features, SqlDialectFeatures.DollarQuotedStrings) && !char.IsDigit(next))
            {
                var tagEnd = DollarTagEnd(sql, i);
                if (tagEnd == Unterminated)
                {
                    return FullyObfuscated;
                }

                var tag = sql.Substring(i, tagEnd - i + 1);
                var close = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
                sb.Append('?');
                i = close < 0 ? length : close + tag.Length;
                continue;
            }

            if (Has(features, SqlDialectFeatures.UuidLiterals) && TryMatchUuid(sql, i, out var uuidEnd))
            {
                sb.Append('?');
                i = uuidEnd;
                continue;
            }

            // Numbers, including a unary minus but never a binary one
            if (ch == '-' && StartsNumber(sql, i + 1) && IsUnaryMinusPosition(sb))
            {
                sb.Append('?');
                i = SkipNumber(sql, i + 1);
                continue;
            }

            if (StartsNumber(sql, i))
            {
                sb.Append('?');
                i = SkipNumber(sql, i);
                continue;
            }

            // Identifiers and keywords
            if (char.IsLetter(ch) || ch == '_')
            {
                var start = i;
                var dollarInIdentifiers = Has(features, SqlDialectFeatures.DollarQuotedStrings);
                while (i < length && IsIdentifierChar(sql[i], dollarInIdentifiers))
                {
                    i++;
                }

                if (Has(features, SqlDialectFeatures.BooleanLiterals) && IsBooleanLiteral(sql, start, i - start))
                {
                    sb.Append('?');
                }
                else
                {
                    sb.Append(sql, start, i - start);
                }

                continue;
            }

            sb.Append(ch);
            i++;
        }

        return sb.ToString();
    }

    private static bool Has(SqlDialectFeatures features, SqlDialectFeatures feature) => (features & feature) != 0;

    private static int EndOfLine(string sql, int i)
    {
        while (i < sql.Length && sql[i] != '\r' && sql[i] != '\n')
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// Returns the index just past the closing quote, sql.Length when an ambiguous backslash escape forces the rest
    /// of the query to be consumed, or Unterminated. A doubled quote is an escaped quote.
    /// </summary>
    private static int SkipQuoted(string sql, int start, bool backslashEscapes)
    {
        var quote = sql[start];
        for (var i = start + 1; i < sql.Length; i++)
        {
            var ch = sql[i];
            if (backslashEscapes && ch == '\\' && i + 1 < sql.Length && sql[i + 1] == quote)
            {
                return sql.Length;
            }

            if (ch == quote)
            {
                if (i + 1 < sql.Length && sql[i + 1] == quote)
                {
                    i++;
                    continue;
                }

                return i + 1;
            }
        }

        return Unterminated;
    }

    /// <summary>
    /// Returns the index of the $ that closes an opening dollar-quote tag ($$ or $tag$), or Unterminated.
    /// </summary>
    private static int DollarTagEnd(string sql, int start)
    {
        var i = start + 1;
        if (i < sql.Length && sql[i] == '$')
        {
            return i;
        }

        if (i < sql.Length && (char.IsLetter(sql[i]) || sql[i] == '_'))
        {
            while (i < sql.Length && IsIdentifierChar(sql[i], false))
            {
                i++;
            }

            if (i < sql.Length && sql[i] == '$')
            {
                return i;
            }
        }

        return Unterminated;
    }

    /// <summary>
    /// Matches 32 hex digits, optionally separated by any dashes and optionally wrapped in braces, not followed by an identifier character.
    /// </summary>
    private static bool TryMatchUuid(string sql, int start, out int end)
    {
        end = start;
        var i = start;
        var braced = sql[i] == '{';
        if (braced)
        {
            i++;
        }

        var digits = 0;
        while (i < sql.Length && digits < UuidHexDigitCount)
        {
            if (IsHexDigit(sql[i]))
            {
                digits++;
                i++;
            }
            else if (sql[i] == '-' && digits > 0 && !(i + 1 < sql.Length && sql[i + 1] == '-'))
            {
                i++;
            }
            else
            {
                break;
            }
        }

        if (digits != UuidHexDigitCount)
        {
            return false;
        }

        if (braced && i < sql.Length && sql[i] == '}')
        {
            i++;
        }

        if (i < sql.Length && IsIdentifierChar(sql[i], false))
        {
            return false;
        }

        end = i;
        return true;
    }

    private static bool StartsNumber(string sql, int i)
    {
        return i < sql.Length && (char.IsDigit(sql[i]) || (sql[i] == '.' && i + 1 < sql.Length && char.IsDigit(sql[i + 1])));
    }

    private static int SkipNumber(string sql, int i)
    {
        while (i < sql.Length && (char.IsDigit(sql[i]) || sql[i] == '.'))
        {
            i++;
        }

        if (i < sql.Length && (sql[i] == 'e' || sql[i] == 'E'))
        {
            var j = i + 1;
            if (j < sql.Length && (sql[j] == '+' || sql[j] == '-'))
            {
                j++;
            }

            if (j < sql.Length && char.IsDigit(sql[j]))
            {
                i = j;
                while (i < sql.Length && char.IsDigit(sql[i]))
                {
                    i++;
                }
            }
        }

        // Postgres 15+ rejects junk after a numeric literal, so trailing letters, digits and underscores (1_000, 0b1010, 0o17, 0xFF) belong to the literal.
        while (i < sql.Length && IsIdentifierChar(sql[i], false))
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// A minus is unary (part of a negative literal) unless it follows an operand.
    /// </summary>
    private static bool IsUnaryMinusPosition(StringBuilder output)
    {
        for (var i = output.Length - 1; i >= 0; i--)
        {
            var ch = output[i];
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            return !(char.IsLetterOrDigit(ch) || ch == '_' || ch == '?' || ch == ')' || ch == '"' || ch == ']' || ch == '`' || ch == '$');
        }

        return true;
    }

    private static bool IsIdentifierChar(char ch, bool dollarAllowed)
    {
        return char.IsLetterOrDigit(ch) || ch == '_' || (dollarAllowed && ch == '$');
    }

    private static bool IsHexDigit(char ch)
    {
        return (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
    }

    private static bool IsBooleanLiteral(string sql, int start, int length)
    {
        switch (length)
        {
            case 4:
                return string.Compare(sql, start, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0
                       || string.Compare(sql, start, "null", 0, 4, StringComparison.OrdinalIgnoreCase) == 0;
            case 5:
                return string.Compare(sql, start, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0;
            default:
                return false;
        }
    }
}
