// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;

namespace NewRelic.Agent.Core.Database;

/// <summary>
/// Replaces SQL literals (strings, numbers, comments and dialect-specific literals) with "?" in a single linear pass.
/// Input that cannot be tokenized safely (an unterminated string, comment or quoted identifier, or a stray comment
/// terminator) is replaced entirely with "?", because over-obfuscating is always preferable to leaking a value. The
/// one exception is a block comment nested before its own closing "*/" (<see cref="SqlLexemeKind.NestedComment"/>):
/// everything up to that point was tokenized without ambiguity, so only the comment itself -- not the output already
/// produced for it -- is masked.
/// Does not use regular expressions: the input is not length-limited and must never backtrack.
/// </summary>
public static class SqlLiteralScanner
{
    private const string FullyObfuscated = "?";
    private const int Unterminated = -1;
    private const int UuidHexDigitCount = 32;

    /// <summary>
    /// What <see cref="TryClassifyLexeme"/> found starting at a given index. Shared by Obfuscate and
    /// <see cref="StripComments"/> so both are driven by the exact same comment/quote/ambiguity rules (branch
    /// order, nested-block-comment handling, per-dialect backslash escapes); each method decides for itself how to
    /// render a given kind (mask with "?", blank with spaces, or copy verbatim).
    /// </summary>
    private enum SqlLexemeKind
    {
        /// <summary>A construct the dialect reads as SQL but that this scanner cannot safely tokenize further (e.g. a
        /// Postgres # operator, MySQL "--" not followed by whitespace). Extends to the end of the input.</summary>
        MaskRestOfQuery,
        EndOfLineComment,
        BlockComment,
        /// <summary>An unterminated block comment, a stray "*/", or an unterminated dollar-quote tag. Extends to the end of the input.</summary>
        AmbiguousComment,
        /// <summary>A block comment whose extent is ambiguous because a second "/*" appears before its first "*/" (block
        /// comments nest in some dialects and not others). Unlike <see cref="AmbiguousComment"/>, its start IS a real,
        /// closed comment -- only how much of what follows it also belongs to the comment is unclear.</summary>
        NestedComment,
        /// <summary>A string literal, Oracle alternative-quoted string, or dollar-quoted string -- literal content.</summary>
        QuotedLiteral,
        /// <summary>A double/backtick/bracket-quoted identifier -- not literal content, kept verbatim by both callers.</summary>
        QuotedIdentifier,
        /// <summary>An unterminated string literal or quoted identifier. Extends to the end of the input.</summary>
        UnterminatedQuoted,
    }

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

        // Branch order matters: the rest-of-query masks before comments, comments before strings, Oracle q-quotes before
        // other quotes, quotes before $ handling, UUIDs before numbers and identifiers, unary minus before numbers.
        // Reordering can desync the scan or leak part of a literal. TryClassifyLexeme owns that order for everything
        // through dollar-quoted strings; UUIDs, numbers and identifiers are specific to obfuscating literals, so they
        // stay here.
        while (i < length)
        {
            if (TryClassifyLexeme(sql, i, features, out var kind, out var end))
            {
                switch (kind)
                {
                    case SqlLexemeKind.MaskRestOfQuery:
                    case SqlLexemeKind.EndOfLineComment:
                    case SqlLexemeKind.BlockComment:
                    case SqlLexemeKind.QuotedLiteral:
                        AppendCapped(sb, '?', maxLength);
                        i = end;
                        continue;

                    case SqlLexemeKind.QuotedIdentifier:
                        AppendCapped(sb, sql, i, end - i, maxLength);
                        i = end;
                        continue;

                    case SqlLexemeKind.NestedComment:
                        // The comment's extent past its first "*/" is ambiguous, but everything up to here was
                        // real, already-tokenized SQL -- keep it (matching ordinary comment handling) and mask
                        // only this ambiguous comment itself, rather than discarding output that is not in doubt.
                        AppendCapped(sb, '?', maxLength);
                        i = end;
                        continue;

                    case SqlLexemeKind.AmbiguousComment:
                    case SqlLexemeKind.UnterminatedQuoted:
                        return FullyObfuscatedWithPrefix(prefix, maxLength);
                }
            }

            var ch = sql[i];

            if (Has(features, SqlDialectFeatures.UuidLiterals) && TryMatchUuid(sql, i, out var uuidEnd))
            {
                AppendCapped(sb, '?', maxLength);
                i = uuidEnd;
                continue;
            }

            // Numbers, including a unary minus but never a binary one
            if (ch == '-' && StartsNumber(sql, i + 1) && IsUnaryMinusPosition(sb, prefixLength, Has(features, SqlDialectFeatures.HashInIdentifiers)))
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
                var hashInIdentifiers = Has(features, SqlDialectFeatures.HashInIdentifiers);
                while (i < length && IsIdentifierChar(sql[i], dollarInIdentifiers || hashInIdentifiers, hashInIdentifiers))
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

    /// <summary>
    /// Returns sql with every comment (--, # when HashComments is set, and /* */) replaced by same-length runs of
    /// spaces. Everything else -- string and identifier-quoted content in particular, even when it contains text
    /// that looks like a comment delimiter -- is returned completely untouched, so (unlike Obfuscate) this never
    /// shortens or otherwise rewrites non-comment text and preserves character offsets throughout.
    ///
    /// Intended for callers that need to locate real code (e.g. to match a bound parameter's placeholder) without
    /// being misled by comment text. It's unnecessary to handle a parameter placeholder that happens to appear
    /// inside a comment specially -- it's simpler and less error-prone to just remove the comment first, since
    /// comments have no effect on the explain plan.
    ///
    /// Returns null when the input cannot be classified with confidence -- an unterminated or ambiguous comment
    /// (see the nested-block-comment handling in TryClassifyLexeme), or an unterminated string or quoted
    /// identifier -- mirroring Obfuscate's policy of refusing to guess rather than risk treating code as a
    /// comment or a comment as code.
    /// </summary>
    public static string StripComments(string sql, SqlDialectFeatures features)
    {
        if (sql == null)
        {
            return null;
        }

        var length = sql.Length;
        var sb = new StringBuilder(length);
        var i = 0;

        while (i < length)
        {
            if (TryClassifyLexeme(sql, i, features, out var kind, out var end))
            {
                switch (kind)
                {
                    case SqlLexemeKind.MaskRestOfQuery:
                    case SqlLexemeKind.EndOfLineComment:
                    case SqlLexemeKind.BlockComment:
                        AppendSpaces(sb, end - i);
                        i = end;
                        continue;

                    case SqlLexemeKind.QuotedLiteral:
                    case SqlLexemeKind.QuotedIdentifier:
                        sb.Append(sql, i, end - i);
                        i = end;
                        continue;

                    case SqlLexemeKind.NestedComment:
                    case SqlLexemeKind.AmbiguousComment:
                    case SqlLexemeKind.UnterminatedQuoted:
                        return null;
                }
            }

            sb.Append(sql[i]);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Classifies the comment/quote/ambiguous construct (if any) starting at sql[i], shared verbatim by Obfuscate
    /// and StripComments so both are governed by the exact same recognition rules. Returns false when sql[i] does
    /// not start one of those constructs -- ordinary code, including the start of a number, identifier, or UUID,
    /// none of which this shared step needs to know about (only Obfuscate treats those specially).
    ///
    /// Branch order matters here exactly as it did in the original single-method scanner: the rest-of-query mask
    /// before comments, comments before strings, Oracle q-quotes before other quotes, quotes before $ handling.
    /// Reordering can desync the scan or leak part of a literal.
    /// </summary>
    private static bool TryClassifyLexeme(string sql, int i, SqlDialectFeatures features, out SqlLexemeKind kind, out int end)
    {
        var length = sql.Length;
        var ch = sql[i];
        var next = i + 1 < length ? sql[i + 1] : '\0';

        if (MasksRestOfQuery(sql, i, ch, next, features))
        {
            kind = SqlLexemeKind.MaskRestOfQuery;
            end = length;
            return true;
        }

        // End-of-line comments
        if ((ch == '-' && next == '-') || (ch == '#' && Has(features, SqlDialectFeatures.HashComments)))
        {
            kind = SqlLexemeKind.EndOfLineComment;
            end = EndOfLine(sql, i);
            return true;
        }

        // Block comments
        if (ch == '/' && next == '*')
        {
            var close = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                kind = SqlLexemeKind.AmbiguousComment;
                end = length;
                return true;
            }

            // Block comments nest in some dialects (Postgres, SQL Server) and not in others, so a second opener before the
            // first closer makes the comment's extent ambiguous: mask the rest rather than risk exposing text read as SQL.
            var nested = sql.IndexOf("/*", i + 2, close - i - 1, StringComparison.Ordinal);
            if (nested >= 0)
            {
                kind = SqlLexemeKind.NestedComment;
                end = length;
                return true;
            }

            kind = SqlLexemeKind.BlockComment;
            end = close + 2;
            return true;
        }

        if (ch == '*' && next == '/')
        {
            kind = SqlLexemeKind.AmbiguousComment;
            end = length;
            return true;
        }

        // Oracle alternative quoting: q'[...]', nq'!...!' and friends
        if (Has(features, SqlDialectFeatures.OracleQuotedStrings) && TryMatchOracleQuote(sql, i, out var oracleQuoteEnd))
        {
            kind = SqlLexemeKind.QuotedLiteral;
            end = oracleQuoteEnd;
            return true;
        }

        // String literals
        if (ch == '\'' || (ch == '"' && !Has(features, SqlDialectFeatures.DoubleQuotedIdentifiers)))
        {
            var strEnd = SkipQuoted(sql, i, Has(features, SqlDialectFeatures.BackslashEscapes), ch);
            if (strEnd == Unterminated)
            {
                kind = SqlLexemeKind.UnterminatedQuoted;
                end = length;
                return true;
            }

            kind = SqlLexemeKind.QuotedLiteral;
            end = strEnd;
            return true;
        }

        // Quoted identifiers
        if (ch == '"'
            || (ch == '`' && Has(features, SqlDialectFeatures.BacktickIdentifiers))
            || (ch == '[' && Has(features, SqlDialectFeatures.BracketIdentifiers)))
        {
            // A doubled closing character is an escaped one: "" in "x", `` in `x`, ]] in [x]
            var idEnd = SkipQuoted(sql, i, false, ch == '[' ? ']' : ch);
            if (idEnd == Unterminated)
            {
                kind = SqlLexemeKind.UnterminatedQuoted;
                end = length;
                return true;
            }

            kind = SqlLexemeKind.QuotedIdentifier;
            end = idEnd;
            return true;
        }

        // Dollar-quoted strings. A $ followed by a digit is a positional parameter, not a quote opener.
        if (ch == '$' && Has(features, SqlDialectFeatures.DollarQuotedStrings) && !char.IsDigit(next))
        {
            var tagEnd = DollarTagEnd(sql, i);
            if (tagEnd == Unterminated)
            {
                // Not really a comment, but the same "cannot safely say what follows" situation, so the two callers
                // react to it exactly as they would an ambiguous comment.
                kind = SqlLexemeKind.AmbiguousComment;
                end = length;
                return true;
            }

            var tag = sql.Substring(i, tagEnd - i + 1);
            var close = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
            kind = SqlLexemeKind.QuotedLiteral;
            end = close < 0 ? length : close + tag.Length;
            return true;
        }

        kind = default;
        end = i;
        return false;
    }

    private static void AppendSpaces(StringBuilder sb, int count)
    {
        for (var n = 0; n < count; n++)
        {
            sb.Append(' ');
        }
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

    /// <summary>
    /// True when the character at i starts text that must mask the rest of the query, because the server reads it as SQL
    /// (or the dialect is unknown) and a quote in it could desync the scan if it were masked only to the end of the line:
    /// a Postgres # (an operator, never a comment), a # followed by whitespace or the end in the Default dialect, a
    /// dollar-quote opener ($$ or $tag$) in the Default dialect, or a MySQL -- that is not followed by whitespace (not a
    /// comment, so 5--1 is arithmetic).
    /// </summary>
    private static bool MasksRestOfQuery(string sql, int i, char ch, char next, SqlDialectFeatures features)
    {
        if (ch == '#')
        {
            return Has(features, SqlDialectFeatures.HashMasksRestOfQuery)
                   || (Has(features, SqlDialectFeatures.HashWhitespaceMasksRestOfQuery) && IsWhiteSpaceOrEnd(sql, i + 1));
        }

        if (ch == '$')
        {
            return Has(features, SqlDialectFeatures.DollarQuoteMasksRestOfQuery) && DollarTagEnd(sql, i) != Unterminated;
        }

        return ch == '-' && next == '-' && Has(features, SqlDialectFeatures.DashDashRequiresWhitespace) && !IsAsciiSpaceOrControlOrEnd(sql, i + 2);
    }

    private static bool IsWhiteSpaceOrEnd(string sql, int i)
    {
        return i >= sql.Length || char.IsWhiteSpace(sql[i]);
    }

    /// <summary>
    /// MySQL ends its -- marker only with an ASCII space or control character. A Unicode space such as NBSP is not one,
    /// so the server reads the text after it as SQL.
    /// </summary>
    private static bool IsAsciiSpaceOrControlOrEnd(string sql, int i)
    {
        return i >= sql.Length || sql[i] <= ' ';
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
    private static int SkipQuoted(string sql, int start, bool backslashEscapes, char quote)
    {
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
            while (i < sql.Length && IsIdentifierChar(sql[i], false, false))
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

        if (i < sql.Length && IsIdentifierChar(sql[i], false, false))
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

        // Trailing letters, digits and underscores (1_000, 0b1010, 0o17, 0xFF, 3.14food) are masked with the literal: keeping
        // them as an identifier could expose part of a value, and several dialects reject such junk after a number anyway.
        while (i < sql.Length && IsIdentifierChar(sql[i], false, false))
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// A minus is unary (part of a negative literal) unless it follows an operand. Only output written after the verbatim
    /// prefix (from outputStart) is considered, so a prefix never changes how the SQL after it is read. When # is legal in
    /// identifiers (Oracle EMP#), a trailing # also ends an operand. Once the output is capped the answer only decides how
    /// the number is consumed, never how quotes are read, so the scan stays in step.
    /// </summary>
    private static bool IsUnaryMinusPosition(StringBuilder output, int outputStart, bool hashInIdentifiers)
    {
        for (var i = output.Length - 1; i >= outputStart; i--)
        {
            var ch = output[i];
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            return !(char.IsLetterOrDigit(ch) || ch == '_' || ch == '?' || ch == ')' || ch == '"' || ch == ']' || ch == '`' || ch == '$'
                     || (hashInIdentifiers && ch == '#'));
        }

        return true;
    }

    private static bool IsIdentifierChar(char ch, bool dollarAllowed, bool hashAllowed)
    {
        return char.IsLetterOrDigit(ch) || ch == '_' || (dollarAllowed && ch == '$') || (hashAllowed && ch == '#');
    }

    /// <summary>
    /// Matches an Oracle alternative-quoted string: [n]q'&lt;d&gt;...&lt;closer&gt;' where d is any character other than
    /// whitespace or a quote. The brackets [ { &lt; ( close with ] } &gt; ); any other d closes with itself. An
    /// unclosed literal consumes the rest of the query. Returns the index just past the literal.
    /// </summary>
    private static bool TryMatchOracleQuote(string sql, int start, out int end)
    {
        end = start;
        var q = start;
        if (sql[q] == 'n' || sql[q] == 'N')
        {
            q++;
        }

        // q must be followed by ' and a delimiter
        if (q + 2 >= sql.Length || (sql[q] != 'q' && sql[q] != 'Q') || sql[q + 1] != '\'')
        {
            return false;
        }

        var delimiter = sql[q + 2];
        if (char.IsWhiteSpace(delimiter) || delimiter == '\'')
        {
            return false;
        }

        // The literal ends at the first closing delimiter that is followed by a quote. Each IndexOf resumes past the previous
        // hit, so the search stays linear.
        var closer = OracleClosingDelimiter(delimiter);
        var close = sql.IndexOf(closer, q + 3);
        while (close >= 0 && !(close + 1 < sql.Length && sql[close + 1] == '\''))
        {
            close = sql.IndexOf(closer, close + 1);
        }

        end = close < 0 ? sql.Length : close + 2;
        return true;
    }

    private static char OracleClosingDelimiter(char opening)
    {
        switch (opening)
        {
            case '[':
                return ']';
            case '{':
                return '}';
            case '<':
                return '>';
            case '(':
                return ')';
            default:
                return opening;
        }
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
