using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NewRelic.SystemInterfaces
{
	public interface INetworkData
	{
		IPAddress GetLocalIPAddress();

		string GetDomainName(INetworkInterfaceData networkInterfaceData);

		INetworkInterfaceData GetActiveNetworkInterface(IPAddress localIPAddress, List<INetworkInterfaceData> networkInterfaces);

		List<INetworkInterfaceData> GetNetworkInterfaceData();
	}

	public class NetworkData : INetworkData
	{
		private INetworkInterfaceData _activeNetworkInterface;
		private IPAddress _localIPAddress;

		public string GetDomainName(INetworkInterfaceData networkInterfaceData)
		{
			try
			{
				return networkInterfaceData?.DnsSuffix ?? string.Empty;
			}
			catch (Exception exception)
			{
				Log.WarnFormat($"Error retrieving domain name from network interface: {0}", exception);
			}

			return string.Empty;
		}

		public IPAddress GetLocalIPAddress()
		{
			try
			{
				if (_localIPAddress != null)
				{
					return _localIPAddress;
				}

				// connect to NR to get active interface, 0 means use next open port, Java does the same
				using (var udpClient = new UdpClient("newrelic.com", 0))
				{
					return _localIPAddress = ((IPEndPoint)udpClient.Client.LocalEndPoint).Address;
				}
			}
			catch (Exception exception)
			{
				Log.WarnFormat("Unable to determine local IP address: {0}", exception.Message);
			}

			return _localIPAddress = IPAddress.None; // 255.255.255.255
		}

		public INetworkInterfaceData GetActiveNetworkInterface(IPAddress localIPAddress, List<INetworkInterfaceData> networkInterfaces)
		{
			if(_activeNetworkInterface != null)
			{
				return _activeNetworkInterface;
			}

			try
			{
				foreach (var networkInterface in networkInterfaces)
				{
					foreach (var ipAddress in networkInterface.UnicastIPAddresses)
					{
						if (ipAddress.Address.Equals(localIPAddress))
						{
							return _activeNetworkInterface = networkInterface;
						}

						continue;
					}
				}
			}
			catch (Exception exception)
			{
				Log.WarnFormat("Unable to get an active network interface: {0}", exception.Message);
			}
			return _activeNetworkInterface = new NetworkInterfaceData(string.Empty, new List<UnicastIPAddressInformation>());
		}

		// Due to .GetAllNetworkInterfaces() and .GetIPProperties().UnicastAddresses not being mockable, this method is not testable on its own.
		// Because of this, the method is built very simply and always return something.
		public List<INetworkInterfaceData> GetNetworkInterfaceData()
		{
			var networkIntefaces = new List<INetworkInterfaceData>();

			try
			{
				var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
				for (var i = 0; i < networkInterfaces.Length; i++)
				{
					var unicastAddresses = networkInterfaces[i].GetIPProperties().UnicastAddresses;
					if (unicastAddresses.Count == 0)
					{
						continue;
					}

					networkIntefaces.Add(
						new NetworkInterfaceData(
							networkInterfaces[i].GetIPProperties().DnsSuffix,
							unicastAddresses)
					);
				}
			}
			catch (NetworkInformationException networkInformationException)
			{
				Log.WarnFormat($"Error getting network interfaces: {0}", networkInformationException);
			}
			catch (Exception exception)
			{
				Log.WarnFormat($"Error capturing unicast addresses from network interface: {0}", exception);
			}

			return networkIntefaces;
		}
	}
}
