// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.Core.SharedInterfaces
{
    public interface IDnsStatic
    {
        string GetHostName();


        IPHostEntry GetHostEntry(string hostNameOrAddres);

        string GetFullHostName();

        List<string> GetIpAddresses();
    }

    public class DnsStatic : IDnsStatic
    {
        private const string Ipv6ScopeIDPattern = @"(%[a-zA-Z\d]*)"; // used to remove the scope from IPv6 addresses
        private readonly INetworkData _networkData;
        private Regex _regex;

        public DnsStatic(INetworkData networkData)
        {
            _networkData = networkData;
            _regex = new Regex(Ipv6ScopeIDPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public string GetHostName()
        {
            return Dns.GetHostName();
        }

        public IPHostEntry GetHostEntry(string hostNameOrAddres)
        {
            return Dns.GetHostEntry(hostNameOrAddres);
        }

        public string GetFullHostName()
        {
            var hostName = GetHostName();
            var activeNetworkInterface = GetActiveNetworkInterface();
            var domainName = _networkData.GetDomainName(activeNetworkInterface);
            if (!string.IsNullOrEmpty(domainName) && !hostName.EndsWith(domainName))
            {
                hostName += $".{domainName}";
            }

            return hostName;
        }

        public List<string> GetIpAddresses()
        {
            var activeNetworkInterface = GetActiveNetworkInterface();
            var ipAddresses = new List<string>();
            foreach (var unicastAddress in activeNetworkInterface.UnicastIPAddresses)
            {
                var ipAddress = unicastAddress.Address.ToString();
                ipAddresses.Add(_regex.Replace(ipAddress, ""));
            }

            return ipAddresses;
        }

        private INetworkInterfaceData GetActiveNetworkInterface()
        {
            var networkInterfaceData = _networkData.GetNetworkInterfaceData();
            var localIPAddress = _networkData.GetLocalIPAddress();
            return _networkData.GetActiveNetworkInterface(localIPAddress, networkInterfaceData);
        }
    }
}
