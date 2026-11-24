// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data.Common;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Extensions.Parsing.ConnectionString;

public class OracleConnectionStringParser : IConnectionStringParser
{
    private const char QuoteChar = '"';
    private const char SingleQuoteChar = '\'';

    private static readonly string[] _protocolPrefixes = { "//", "tcp://", "tcps://" };
    private static readonly char[] _addressEndChars = { StringSeparators.QuestionMarkChar, StringSeparators.PathSeparatorChar };
    private static readonly char[] _hostEndChars = { StringSeparators.CommaChar, StringSeparators.ColonChar, StringSeparators.SemiColonChar };
    private static readonly char[] _portEndChars = { StringSeparators.CommaChar, StringSeparators.SemiColonChar };

    private static readonly List<string> _hostKeys = new List<string> { "server", "data source", "host", "dbq" };

    private readonly DbConnectionStringBuilder _connectionStringBuilder;

    public OracleConnectionStringParser(string connectionString)
    {
        _connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
    }

    public ConnectionInfo GetConnectionInfo(string utilizationHostName)
    {
        var dataSource = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
        if (dataSource == null)
        {
            return new ConnectionInfo(null, "default", null);
        }

        string host;
        string portString;
        try
        {
            ParseDataSource(dataSource, out host, out portString);
        }
        catch (Exception e)
        {
            Log.Debug(e, "Unhandled exception in OracleConnectionStringParser.GetConnectionInfo");
            return new ConnectionInfo(null, -1, null);
        }

        // No port token means the default port; a port token that is not a number is unknown
        if (portString == null)
        {
            return new ConnectionInfo(host, "default", null);
        }

        return new ConnectionInfo(host, int.TryParse(portString, out var port) ? port : -1, null);
    }

    // Patterns handled:
    //   [username/password@][[tcp:|tcps:]//]host[:port][{,|;}host[:port]...][/service][?parameters]
    //   [username/password@]([descriptor])
    private static void ParseDataSource(string value, out string host, out string portString)
    {
        var connectIdentifier = value.Trim();

        // A user name cannot start with '(', so a leading '(' is a descriptor with no credentials
        if (!connectIdentifier.StartsWith("(", StringComparison.Ordinal))
        {
            connectIdentifier = connectIdentifier.Substring(FindCredentialsSeparator(connectIdentifier) + 1).Trim();
        }

        if (connectIdentifier.StartsWith("(", StringComparison.Ordinal))
        {
            ParseDescriptor(connectIdentifier, out host, out portString);
        }
        else
        {
            ParseEasyConnect(connectIdentifier, out host, out portString);
        }
    }

    // The first '@' outside double quotes, or -1 if Easy Connect parameters or the end come first.
    // An unquoted '?' starts parameters only when an '=' follows it before the next '@'.
    private static int FindCredentialsSeparator(string value)
    {
        var inQuotes = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == QuoteChar)
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && c == StringSeparators.AtSignChar)
            {
                return i;
            }
            else if (!inQuotes && c == StringSeparators.QuestionMarkChar && StartsParameters(value, i))
            {
                return -1;
            }
        }

        return -1;
    }

    private static bool StartsParameters(string value, int questionMarkIndex)
    {
        var equalIndex = value.IndexOf(StringSeparators.EqualSignChar, questionMarkIndex);
        var atIndex = value.IndexOf(StringSeparators.AtSignChar, questionMarkIndex);
        return equalIndex >= 0 && (atIndex < 0 || equalIndex < atIndex);
    }

    // HOST and PORT come from the same ADDRESS, in either order
    private static void ParseDescriptor(string descriptor, out string host, out string portString)
    {
        host = null;
        portString = null;

        foreach (var section in SplitOutsideQuotes(descriptor))
        {
            var equalIndex = section.IndexOf(StringSeparators.EqualSignChar);
            if (equalIndex < 0) continue;

            var key = section.Substring(0, equalIndex).Trim();
            if (key.Equals("ADDRESS", StringComparison.OrdinalIgnoreCase))
            {
                if (host != null) break;

                portString = null;
            }
            else if (key.Equals("HOST", StringComparison.OrdinalIgnoreCase))
            {
                host = ReadUntil(section, equalIndex + 1, StringSeparators.CloseParen);
            }
            else if (key.Equals("PORT", StringComparison.OrdinalIgnoreCase))
            {
                portString = ReadUntil(section, equalIndex + 1, StringSeparators.CloseParen);
            }
        }
    }

    // Splits on '(' outside single or double quotes, so a quoted value such as
    // SSL_SERVER_CERT_DN="CN=x(HOST=y)" stays in one section
    private static IEnumerable<string> SplitOutsideQuotes(string descriptor)
    {
        var quote = '\0';
        var start = 0;
        for (var i = 0; i < descriptor.Length; i++)
        {
            var c = descriptor[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
            }
            else if (c == QuoteChar || c == SingleQuoteChar)
            {
                quote = c;
            }
            else if (c == StringSeparators.OpenParenChar)
            {
                yield return descriptor.Substring(start, i - start);
                start = i + 1;
            }
        }

        yield return descriptor.Substring(start);
    }

    // host[:port][/service][?parameters], where host may be a bracketed IPv6 address
    private static void ParseEasyConnect(string connectIdentifier, out string host, out string portString)
    {
        foreach (var prefix in _protocolPrefixes)
        {
            if (connectIdentifier.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                connectIdentifier = connectIdentifier.Substring(prefix.Length);
                break;
            }
        }

        var addressEnd = connectIdentifier.IndexOfAny(_addressEndChars);
        var addresses = addressEnd < 0 ? connectIdentifier : connectIdentifier.Substring(0, addressEnd);

        host = addresses.StartsWith("[", StringComparison.Ordinal)
            ? addresses.Substring(1, addresses.IndexOf(']') - 1)
            : ReadUntil(addresses, 0, _hostEndChars);
        portString = FindPort(addresses);
    }

    // The text after the first ':' outside brackets; null if a ';' (the next address) comes first
    private static string FindPort(string addresses)
    {
        var inBrackets = false;
        for (var i = 0; i < addresses.Length; i++)
        {
            var c = addresses[i];
            if (c == '[')
            {
                inBrackets = true;
            }
            else if (c == ']')
            {
                inBrackets = false;
            }
            else if (!inBrackets && c == StringSeparators.SemiColonChar)
            {
                return null;
            }
            else if (!inBrackets && c == StringSeparators.ColonChar)
            {
                return ReadUntil(addresses, i + 1, _portEndChars);
            }
        }

        return null;
    }

    private static string ReadUntil(string text, int start, char[] stopChars)
    {
        var end = text.IndexOfAny(stopChars, start);
        return (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).Trim();
    }
}
