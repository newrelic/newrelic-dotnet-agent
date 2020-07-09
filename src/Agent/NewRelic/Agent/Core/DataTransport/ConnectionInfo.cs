﻿using System;
using System.Net;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
	public class ConnectionInfo
	{
		[NotNull]
		public readonly String Host;
		public readonly UInt32 Port;
		public readonly String HttpProtocol;
		[CanBeNull]
		public readonly String ProxyHost;
		[CanBeNull]
		public readonly String ProxyUriPath;
		public readonly Int32 ProxyPort;
		[CanBeNull]
		public readonly String ProxyUsername;
		[CanBeNull]
		public readonly String ProxyPassword;
		[CanBeNull]
		public readonly String ProxyDomain;
		[CanBeNull]
		public readonly WebProxy Proxy;

		public ConnectionInfo([NotNull] IConfiguration configuration)
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

		public ConnectionInfo([NotNull] IConfiguration configuration, [CanBeNull] String redirectHost)
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

		[CanBeNull]
		private static WebProxy GetWebProxy([CanBeNull] String proxyHost, String proxyUriPath, Int32 proxyPort, [CanBeNull] String proxyUsername, [CanBeNull] String proxyPassword, [CanBeNull] String proxyDomain)
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

		[CanBeNull]
		private String GetProxyAddress()
		{
			if (ProxyHost == null)
				return null;

			var host = GetProxyHostWithoutCredentials(ProxyHost);
			var port = ProxyPort;
			var uriPath = String.IsNullOrEmpty(ProxyUriPath) ? String.Empty : "/" + ProxyUriPath.TrimStart('/');

			return String.Format("{0}:{1}{2}", host, port, uriPath);
		}

		[NotNull]
		private String GetProxyHostWithoutCredentials([NotNull] String proxyHost)
		{
			var atIndexInProxyHost = proxyHost.IndexOf('@');
			if (atIndexInProxyHost < 0 || atIndexInProxyHost >= proxyHost.Length)
				return proxyHost;

			return proxyHost.Substring(atIndexInProxyHost + 1);
		}
	}
}
