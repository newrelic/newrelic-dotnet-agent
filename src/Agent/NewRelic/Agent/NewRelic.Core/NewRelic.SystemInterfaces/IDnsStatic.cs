/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Net;

namespace NewRelic.SystemInterfaces
{
    public interface IDnsStatic
    {
        string GetHostName();
        IPHostEntry GetHostEntry(string hostNameOrAddres);
    }

    public class DnsStatic : IDnsStatic
    {
        public string GetHostName()
        {
            return Dns.GetHostName();
        }

        public IPHostEntry GetHostEntry(string hostNameOrAddres)
        {
            return Dns.GetHostEntry(hostNameOrAddres);
        }
    }
}
