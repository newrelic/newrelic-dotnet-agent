// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.RegularExpressions;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
    public class ConnectionInfo
    {
        public readonly string Host;
        public readonly int Port;
        public readonly string HttpProtocol;
        public readonly string ProxyHost;
        public readonly string ProxyUriPath;
        public readonly int ProxyPort;
        public readonly string ProxyUsername;
        public readonly string ProxyPassword;
        public readonly string ProxyDomain;
        public readonly WebProxy Proxy;

        private static readonly Regex accountRegionRegex = new Regex("^.+?x");


        public ConnectionInfo(IConfiguration configuration)
        {
            Host = GetCollectorHost(configuration);
            Port = configuration.CollectorPort;
            HttpProtocol = configuration.CollectorHttpProtocol;
            ProxyHost = configuration.ProxyHost;
            ProxyUriPath = configuration.ProxyUriPath;
            ProxyPort = configuration.ProxyPort;
            ProxyUsername = configuration.ProxyUsername;
            ProxyPassword = configuration.ProxyPassword;
            ProxyDomain = configuration.ProxyDomain;

            Proxy = GetWebProxy(ProxyHost, ProxyUriPath, ProxyPort, ProxyUsername, ProxyPassword, ProxyDomain);
        }

        public ConnectionInfo(IConfiguration configuration, string redirectHost)
        {
            Host = redirectHost ?? GetCollectorHost(configuration);
            Port = configuration.CollectorPort;
            HttpProtocol = configuration.CollectorHttpProtocol;
            ProxyHost = configuration.ProxyHost;
            ProxyUriPath = configuration.ProxyUriPath;
            ProxyPort = configuration.ProxyPort;
            ProxyUsername = configuration.ProxyUsername;
            ProxyPassword = configuration.ProxyPassword;
            ProxyDomain = configuration.ProxyDomain;

            Proxy = GetWebProxy(ProxyHost, ProxyUriPath, ProxyPort, ProxyUsername, ProxyPassword, ProxyDomain);
        }

        private static string GetCollectorHost(IConfiguration configuration)
        {
            const string defaultCollectorUrl = "collector.newrelic.com";
            const string regionAwareDefaultCollectorUrl = "collector.nr-data.net";
            const char domainSeparator = '.';
            const char regionSeparator = 'x';

            if (string.IsNullOrEmpty(configuration.CollectorHost))
            {
                return defaultCollectorUrl;
            }

            if (configuration.AgentLicenseKey != null)
            {
                var match = accountRegionRegex.Match(configuration.AgentLicenseKey);

                if (match.Success)
                {
                    var regionSegment = match.Value.TrimEnd(regionSeparator);
                    var collectorUrlRegionStartPosition = regionAwareDefaultCollectorUrl.IndexOf(domainSeparator) + 1;
                    var regionAwareCollectorUrl = regionAwareDefaultCollectorUrl.Insert(collectorUrlRegionStartPosition, regionSegment + domainSeparator);
                    return regionAwareCollectorUrl;
                }
            }

            return defaultCollectorUrl;
        }

        private static WebProxy GetWebProxy(string proxyHost, string proxyUriPath, int proxyPort, string proxyUsername, string proxyPassword, string proxyDomain)
        {
            if (string.IsNullOrEmpty(proxyHost))
                return null;

            var proxyUri = string.IsNullOrEmpty(proxyUriPath) ? $"{proxyHost}:{proxyPort}" : $"{proxyHost}:{proxyPort}/{proxyUriPath.TrimStart('/')}";
            var webProxy = new WebProxy(proxyUri);
            if (proxyUsername != null)
                webProxy.Credentials = new NetworkCredential(proxyUsername, proxyPassword, proxyDomain);

            return webProxy;
        }

        public override string ToString()
        {
            var proxyAddress = GetProxyAddress();
            var proxyInformation = proxyAddress != null ? string.Format(" (Proxy: {0})", proxyAddress) : null;
            return string.Format("{0}:{1}{2}", Host, Port, proxyInformation);
        }
        private string GetProxyAddress()
        {
            if (ProxyHost == null)
                return null;

            var host = GetProxyHostWithoutCredentials(ProxyHost);
            var port = ProxyPort;
            var uriPath = string.IsNullOrEmpty(ProxyUriPath) ? string.Empty : "/" + ProxyUriPath.TrimStart('/');

            return string.Format("{0}:{1}{2}", host, port, uriPath);
        }
        private string GetProxyHostWithoutCredentials(string proxyHost)
        {
            var atIndexInProxyHost = proxyHost.IndexOf('@');
            if (atIndexInProxyHost < 0 || atIndexInProxyHost >= proxyHost.Length)
                return proxyHost;

            return proxyHost.Substring(atIndexInProxyHost + 1);
        }
    }
}
