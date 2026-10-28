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
        return Obfuscate(sql, 0, null, int.MaxValue, features);
    }

    /// <summary>
    /// Returns prefix + Obfuscate(sql.Substring(startIndex), features), truncated to maxLength, without copying sql.
    /// The prefix is written verbatim. Output stops growing at maxLength, but the scan still runs to the end of sql, so
    /// input that cannot be tokenized safely anywhere after startIndex (even beyond maxLength) still yields prefix + "?".
    /// </summary>
    public static string Obfuscate(string sql, int startIndex, string prefix, int maxLength, SqlDialectFeatures features)
    {
        if (sql == null)
        {
            return null;
        }

        // Out-of-range arguments mean the prefix boundary can't be trusted, so mask everything rather than throw into the caller.
        if (startIndex < 0 || startIndex > sql.Length || maxLength < 0)
        {
            return FullyObfuscated;
        }

        var prefixLength = prefix?.Length ?? 0;
        var sb = new StringBuilder(prefix, Math.Min(sql.Length - startIndex + prefixLength, maxLength));
        var length = sql.Length;
        var i = startIndex;

        // Branch order matters: the Postgres # mask before comments, comments before strings, quotes before $ handling,
        // UUIDs before numbers and identifiers, unary minus before numbers. Reordering can desync the scan or leak part
        // of a literal.
        while (i < length)
        {
            var ch = sql[i];
            var next = i + 1 < length ? sql[i + 1] : '\0';

            // Postgres has no # comment, so a # masks the rest of the query: a string opened after it could span lines and desync the scan.
            if (ch == '#' && Has(features, SqlDialectFeatures.HashMasksRestOfQuery))
            {
                AppendCapped(sb, '?', maxLength);
                i = length;
                continue;
            }

            // End-of-line comments
            if ((ch == '-' && next == '-') || (ch == '#' && Has(features, SqlDialectFeatures.HashComments)))
            {
                AppendCapped(sb, '?', maxLength);
                i = EndOfLine(sql, i);
                continue;
            }

            // Block comments
            if (ch == '/' && next == '*')
            {
                var close = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    return FullyObfuscatedWithPrefix(prefix, maxLength);
                }

                // Postgres block comments nest, so a second opener before the first closer makes the extent ambiguous: mask the rest.
                var nested = sql.IndexOf("/*", i + 2, close - i - 1, StringComparison.Ordinal);
                AppendCapped(sb, '?', maxLength);
                i = nested >= 0 ? length : close + 2;
                continue;
            }

            if (ch == '*' && next == '/')
            {
                return FullyObfuscatedWithPrefix(prefix, maxLength);
            }

            // String literals
            if (ch == '\'' || (ch == '"' && !Has(features, SqlDialectFeatures.DoubleQuotedIdentifiers)))
            {
                i = SkipQuoted(sql, i, Has(features, SqlDialectFeatures.BackslashEscapes));
                if (i == Unterminated)
                {
                    return FullyObfuscatedWithPrefix(prefix, maxLength);
                }

                AppendCapped(sb, '?', maxLength);
                continue;
            }

            // Quoted identifiers are kept verbatim
            if (ch == '"')
            {
                var end = SkipQuoted(sql, i, false);
                if (end == Unterminated)
                {
                    return FullyObfuscatedWithPrefix(prefix, maxLength);
                }

                AppendCapped(sb, sql, i, end - i, maxLength);
                i = end;
                continue;
            }

            // Dollar-quoted strings. A $ followed by a digit is a positional parameter: the $ passes through and the digits become ?.
            if (ch == '$' && Has(features, SqlDialectFeatures.DollarQuotedStrings) && !char.IsDigit(next))
            {
                var tagEnd = DollarTagEnd(sql, i);
                if (tagEnd == Unterminated)
                {
                    return FullyObfuscatedWithPrefix(prefix, maxLength);
                }

                var tag = sql.Substring(i, tagEnd - i + 1);
                var close = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
                AppendCapped(sb, '?', maxLength);
                i = close < 0 ? length : close + tag.Length;
                continue;
            }

            if (Has(features, SqlDialectFeatures.UuidLiterals) && TryMatchUuid(sql, i, out var uuidEnd))
            {
                AppendCapped(sb, '?', maxLength);
                i = uuidEnd;
                continue;
            }

            // Numbers, including a unary minus but never a binary one
            if (ch == '-' && StartsNumber(sql, i + 1) && IsUnaryMinusPosition(sb, prefixLength))
            {
                AppendCapped(sb, '?', maxLength);
                i = SkipNumber(sql, i + 1);
                continue;
            }

            if (StartsNumber(sql, i))
            {
                AppendCapped(sb, '?', maxLength);
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
                    AppendCapped(sb, '?', maxLength);
                }
                else
                {
                    AppendCapped(sb, sql, start, i - start, maxLength);
                }

                continue;
            }

            AppendCapped(sb, ch, maxLength);
            i++;
        }

        return sb.ToString(0, Math.Min(sb.Length, maxLength));
    }

    private static string FullyObfuscatedWithPrefix(string prefix, int maxLength)
    {
        var result = prefix + FullyObfuscated;
        return result.Length > maxLength ? result.Substring(0, maxLength) : result;
    }

    /// <summary>
    /// Appends only while the output is under maxLength, so a huge statement never builds more than the truncated result.
    /// </summary>
    private static void AppendCapped(StringBuilder sb, char ch, int maxLength)
    {
        if (sb.Length < maxLength)
        {
            sb.Append(ch);
        }
    }

    private static void AppendCapped(StringBuilder sb, string sql, int start, int count, int maxLength)
    {
        var room = maxLength - sb.Length;
        if (room > 0)
        {
            sb.Append(sql, start, Math.Min(count, room));
        }
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
    /// A minus is unary (part of a negative literal) unless it follows an operand. Only output written after the verbatim
    /// prefix (from outputStart) is considered, so a prefix never changes how the SQL after it is read. Once the output is
    /// capped the answer only decides how the number is consumed, never how quotes are read, so the scan stays in step.
    /// </summary>
    private static bool IsUnaryMinusPosition(StringBuilder output, int outputStart)
    {
        for (var i = output.Length - 1; i >= outputStart; i--)
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
