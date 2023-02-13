// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using System.Text;

namespace NewRelic.Agent.Core.Utilities
{
    public static class SecurityProtocolTypeExtensions
    {
        /// <summary>
        /// Have to define this enum privately, as all possible values aren't represented in System.Net.SecurityProtocolType,
        /// depending on which .NET version is targeted. The hex values come from System.Security.Authentication.SslProtocols enum
        /// and/or System.Net.SecurityProtocolType enum (same values in both places)
        /// </summary>
        private enum InternalSecurityProtocolType
        {
            Ssl2 = 0x000C,
            Ssl3 = 0x0030,
            Tls10 = 0x00C0,
            Tls11 = 0x0300,
            Tls12 = 0x0C00,
            Tls13 = 0x3000
        }

        /// <summary>
        /// Convert a SecurityProtocolType enum value to a "friendly" string
        /// </summary>
        /// <param name="protocolType"></param>
        /// <returns></returns>
        public static string ToFriendlyString(this SecurityProtocolType protocolType)
        {
            if (protocolType == 0) 
            {
                return "Using System Default Settings";
            }

            var sb = new StringBuilder();
            foreach (int flag in Enum.GetValues(typeof(InternalSecurityProtocolType)))
            {
                if (((int)protocolType & flag) == flag)
                {
                    if (sb.Length > 0)
                        sb.Append(", ");

                    switch ((InternalSecurityProtocolType)flag)
                    {
                        case InternalSecurityProtocolType.Ssl2:
                            sb.Append("SSL 2");
                            break;
                        case InternalSecurityProtocolType.Ssl3:
                            sb.Append("SSL 3");
                            break;
                        case InternalSecurityProtocolType.Tls10:
                            sb.Append("TLS 1.0");
                            break;
                        case InternalSecurityProtocolType.Tls11:
                            sb.Append("TLS 1.1");
                            break;
                        case InternalSecurityProtocolType.Tls12:
                            sb.Append("TLS 1.2");
                            break;
                        case InternalSecurityProtocolType.Tls13:
                            sb.Append("TLS 1.3");
                            break;
                    }
                }
            }

            return sb.ToString();
        }
    }
}
