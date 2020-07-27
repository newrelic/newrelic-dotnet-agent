using System;
using System.Net;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
    public class ConnectionInfo
    {
        public readonly String Host;
        public readonly UInt32 Port;
        public readonly String HttpProtocol;
        public readonly String ProxyHost;
        public readonly String ProxyUriPath;
        public readonly Int32 ProxyPort;
        public readonly String ProxyUsername;
        public readonly String ProxyPassword;
        public readonly String ProxyDomain;
        public readonly WebProxy Proxy;

        public ConnectionInfo(IConfiguration configuration)
        {
            Host = configuration.CollectorHost;
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

        public ConnectionInfo(IConfiguration configuration, String redirectHost)
        {
            Host = redirectHost ?? configuration.CollectorHost;
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
        private static WebProxy GetWebProxy(String proxyHost, String proxyUriPath, Int32 proxyPort, String proxyUsername, String proxyPassword, String proxyDomain)
        {
            if (String.IsNullOrEmpty(proxyHost))
                return null;

            var proxyUri = String.IsNullOrEmpty(proxyUriPath) ? $"{proxyHost}:{proxyPort}" : $"{proxyHost}:{proxyPort}/{proxyUriPath.TrimStart('/')}";
            var webProxy = new WebProxy(proxyUri);
            if (proxyUsername != null)
                webProxy.Credentials = new NetworkCredential(proxyUsername, proxyPassword, proxyDomain);

            return webProxy;
        }

        public override String ToString()
        {
            var proxyAddress = GetProxyAddress();
            var proxyInformation = proxyAddress != null ? String.Format(" (Proxy: {0})", proxyAddress) : null;
            return String.Format("{0}:{1}{2}", Host, Port, proxyInformation);
        }
        private String GetProxyAddress()
        {
            if (ProxyHost == null)
                return null;

            var host = GetProxyHostWithoutCredentials(ProxyHost);
            var port = ProxyPort;
            var uriPath = String.IsNullOrEmpty(ProxyUriPath) ? String.Empty : "/" + ProxyUriPath.TrimStart('/');

            return String.Format("{0}:{1}{2}", host, port, uriPath);
        }
        private String GetProxyHostWithoutCredentials(String proxyHost)
        {
            var atIndexInProxyHost = proxyHost.IndexOf('@');
            if (atIndexInProxyHost < 0 || atIndexInProxyHost >= proxyHost.Length)
                return proxyHost;

            return proxyHost.Substring(atIndexInProxyHost + 1);
        }
    }
}
