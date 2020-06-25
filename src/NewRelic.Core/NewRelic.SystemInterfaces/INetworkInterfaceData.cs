/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace NewRelic.SystemInterfaces
{
    public interface INetworkInterfaceData
    {
        string DnsSuffix { get; }
        ICollection<UnicastIPAddressInformation> UnicastIPAddresses { get; }
    }

    public class NetworkInterfaceData : INetworkInterfaceData
    {
        public NetworkInterfaceData(string dnsSuffix, ICollection<UnicastIPAddressInformation> ipAddresses)
        {
            DnsSuffix = dnsSuffix;
            UnicastIPAddresses = ipAddresses;
        }

        public string DnsSuffix { get; private set; }

        public ICollection<UnicastIPAddressInformation> UnicastIPAddresses { get; private set; }
    }
}
