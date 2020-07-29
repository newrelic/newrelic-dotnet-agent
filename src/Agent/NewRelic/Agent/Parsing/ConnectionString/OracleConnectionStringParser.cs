using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace NewRelic.Parsing.ConnectionString
{
    public class OracleConnectionStringParser : IConnectionStringParser
    {
        private static readonly List<string> _hostKeys = new List<string> { "server", "data source", "host", "dbq" };

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public OracleConnectionStringParser(string connectionString)
        {
            _connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        public ConnectionInfo GetConnectionInfo()
        {
            var host = ParseHost();
            var portPathOrId = ParsePortPathOrId();
            return new ConnectionInfo(host, portPathOrId, null);
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

            if (host.Contains('('))
            {
                var sections = host.Split('(');
                foreach (var section in sections)
                {
                    if (!section.ToLowerInvariant().Contains("host=")) continue;

                    var startOfValue = section.IndexOf('=') + 1;
                    return section.Substring(startOfValue).Replace(")", string.Empty);
                }
            }
            else if (host.Contains("@"))
            {
                var sections = host.Split('/');
                var initialHostSection = sections[1];
                var secondaryHostSection = sections[3];

                var possibleHost = initialHostSection.Substring(initialHostSection.IndexOf('@') + 1);
                if (!string.IsNullOrEmpty(possibleHost))
                {
                    var colonLocation = possibleHost.IndexOf(':');
                    return colonLocation == -1 ? possibleHost : possibleHost.Substring(0, colonLocation);
                }

                var endOfValue = secondaryHostSection.IndexOf(':');
                possibleHost = (endOfValue > -1) ? secondaryHostSection.Substring(0, secondaryHostSection.IndexOf(':')) : secondaryHostSection;
                if (!string.IsNullOrEmpty(possibleHost)) return possibleHost;

                return null;
            }
            else
            {
                var stops = new[] { ':', '/' };
                var endOfHostname = host.IndexOfAny(stops);
                return endOfHostname == -1 ? host : host.Substring(0, endOfHostname);
            }

            return null;
        }

        private string ParsePortPathOrId()
        {
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            if (host.Contains('('))
            {
                var sections = host.Split('(');
                foreach (var section in sections)
                {
                    if (!section.ToLowerInvariant().Contains("port=")) continue;

                    var startOfValue = section.IndexOf('=') + 1;
                    return section.Substring(startOfValue).Replace(")", string.Empty);
                }
            }

            else if (host.Contains('@'))
            {
                var sections = host.Split('/');
                var initialPortSection = sections[1];
                var secondaryPortSection = sections[3];

                var startOfValue = initialPortSection.IndexOf(':');
                if (startOfValue > -1) return initialPortSection.Substring(startOfValue + 1);

                startOfValue = secondaryPortSection.IndexOf(':');
                if (startOfValue > -1) return secondaryPortSection.Substring(startOfValue + 1);

                return "default";
            }
            else
            {
                var startOfValue = host.IndexOf(':') + 1;
                var endOfValue = host.IndexOf("/", startOfValue);

                if (endOfValue == -1) endOfValue = host.Length;

                return host.Substring(startOfValue, endOfValue - startOfValue);
            }

            return null;
        }
    }
}
