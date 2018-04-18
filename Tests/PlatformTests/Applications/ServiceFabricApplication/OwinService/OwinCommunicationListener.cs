using System;
using System.Fabric;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Microsoft.ServiceFabric.Services.Communication.Runtime;

namespace OwinService
{
	public class OwinCommunicationListener : ICommunicationListener
	{
		private readonly IOwinAppBuilder _startup;
		private readonly string _appRoot;
		private readonly StatelessServiceContext _parameters;

		private string _listeningAddress;

		private IDisposable _serverHandle;

		public OwinCommunicationListener(string appRoot, IOwinAppBuilder startup, StatelessServiceContext serviceInitializationParameters)
		{
			_startup = startup;
			_appRoot = appRoot;
			_parameters = serviceInitializationParameters;
		}

		public Task<string> OpenAsync(CancellationToken cancellationToken)
		{
			var serviceEndpoint =
				_parameters
					.CodePackageActivationContext
					.GetEndpoint("ServiceEndpoint");

			var port = serviceEndpoint.Port;
			var root =
				String.IsNullOrWhiteSpace(_appRoot)
					? String.Empty
					: _appRoot.TrimEnd('/') + '/';

			_listeningAddress = String.Format(
				CultureInfo.InvariantCulture,
				"http://+:{0}/{1}",
				port,
				root
			);
			_serverHandle = WebApp.Start(
				_listeningAddress,
				appBuilder => _startup.Configuration(appBuilder)
			);

			var publishAddress = _listeningAddress.Replace(
				"+",
				FabricRuntime.GetNodeContext().IPAddressOrFQDN
			);

			ServiceEventSource.Current.Message("Listening on {0}", publishAddress);
			return Task.FromResult(publishAddress);
		}

		public void Abort()
		{
			StopWebServer();
		}

		public Task CloseAsync(CancellationToken cancellationToken)
		{
			StopWebServer();
			return Task.FromResult(true);
		}

		private void StopWebServer()
		{
			if (_serverHandle == null)
				return;

			try
			{
				_serverHandle.Dispose();
			}
			catch (ObjectDisposedException) { }
		}
	}
}