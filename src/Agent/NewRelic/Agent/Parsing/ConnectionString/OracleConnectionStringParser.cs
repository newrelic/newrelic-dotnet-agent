// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using NewRelic.Agent.Helpers;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Parsing.ConnectionString
{
    public class OracleConnectionStringParser : IConnectionStringParser
    {
        private static readonly string _closeParen = new string(StringSeparators.CloseParen);
        private static readonly char[] _stopChars = { StringSeparators.ColonChar, StringSeparators.PathSeparatorChar };

        private static readonly List<string> _hostKeys = new List<string> { "server", "data source", "host", "dbq" };

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public OracleConnectionStringParser(string connectionString)
        {
            _connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        public ConnectionInfo GetConnectionInfo(string utilizationHostName)
        {
            var host = ParseHost();
            var portStr = ParsePortString();
            if (string.IsNullOrEmpty(portStr))
            {
                return new ConnectionInfo(DatastoreVendor.Oracle.ToKnownName(), host, "default", null);
            }
            int port;
            if (!int.TryParse(portStr, out port))
            {
                port = -1;
            }
            return new ConnectionInfo(DatastoreVendor.Oracle.ToKnownName(), host, port, null);
        }

        private string ParseHost()
        {
            // Example of want we would need to process:
            // (DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=MyHost)(PORT=MyPort)))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=MyOracleSID)))
            // 111.21.31.99:1521/XE
            // username/password@myserver[:1521]/myservice:dedicated/instancename
            // username/password@//myserver:1521/my.service.com;
            // serverName

            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            if (host.Contains(StringSeparators.OpenParenChar))
            {
                var sections = host.Split(StringSeparators.OpenParen);
                foreach (var section in sections)
                {
                    if (!section.ToLowerInvariant().Contains("host=")) continue;

                    var startOfValue = section.IndexOf(StringSeparators.EqualSignChar) + 1;
                    return section.Substring(startOfValue).Replace(_closeParen, string.Empty);
                }
            }
            else if (host.Contains(StringSeparators.AtSignChar))
            {
                var sections = host.Split(StringSeparators.PathSeparator);
                var initialHostSection = sections[1];
                var secondaryHostSection = sections[3];

                var possibleHost = initialHostSection.Substring(initialHostSection.IndexOf(StringSeparators.AtSignChar) + 1);
                if (!string.IsNullOrEmpty(possibleHost))
                {
                    var colonLocation = possibleHost.IndexOf(StringSeparators.ColonChar);
                    return colonLocation == -1 ? possibleHost : possibleHost.Substring(0, colonLocation);
                }

                var endOfValue = secondaryHostSection.IndexOf(StringSeparators.ColonChar);
                possibleHost = (endOfValue > -1) ? secondaryHostSection.Substring(0, secondaryHostSection.IndexOf(StringSeparators.ColonChar)) : secondaryHostSection;
                if (!string.IsNullOrEmpty(possibleHost)) return possibleHost;

                return null;
            }
            else
            {
                var endOfHostname = host.IndexOfAny(_stopChars);
                return endOfHostname == -1 ? host : host.Substring(0, endOfHostname);
            }

            return null;
        }

        private string ParsePortString()
        {
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            if (host.Contains(StringSeparators.OpenParenChar))
            {
                var sections = host.Split(StringSeparators.OpenParen);
                foreach (var section in sections)
                {
                    if (!section.ToLowerInvariant().Contains("port=")) continue;

                    var startOfValue = section.IndexOf(StringSeparators.EqualSignChar) + 1;
                    return section.Substring(startOfValue).Replace(_closeParen, string.Empty);
                }
            }

            else if (host.Contains(StringSeparators.AtSignChar))
            {
                var sections = host.Split(StringSeparators.PathSeparator);
                var initialPortSection = sections[1];
                var secondaryPortSection = sections[3];

                var startOfValue = initialPortSection.IndexOf(StringSeparators.ColonChar);
                if (startOfValue > -1) return initialPortSection.Substring(startOfValue + 1);

                startOfValue = secondaryPortSection.IndexOf(StringSeparators.ColonChar);
                if (startOfValue > -1) return secondaryPortSection.Substring(startOfValue + 1);

                return null;
            }
            else
            {
                var startOfValue = host.IndexOf(StringSeparators.ColonChar) + 1;
                var endOfValue = host.IndexOf(StringSeparators.PathSeparatorChar, startOfValue);

                if (endOfValue == -1) endOfValue = host.Length;

                return host.Substring(startOfValue, endOfValue - startOfValue);
            }

            return null;
        }
    }
}
