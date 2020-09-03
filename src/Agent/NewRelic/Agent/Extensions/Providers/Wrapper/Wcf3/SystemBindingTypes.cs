// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.MsmqIntegration;

namespace NewRelic.Providers.Wrapper.Wcf3
{
    public static class SystemBindingTypes
    {
        private static List<Type> _systemBindingTypes = new List<Type>
        {
            typeof(BasicHttpBinding),
            typeof(WSHttpBinding),
            typeof(WSDualHttpBinding),
            typeof(WSFederationHttpBinding),
            typeof(NetHttpBinding),
            typeof(NetHttpsBinding),
            typeof(NetTcpBinding),
            typeof(NetNamedPipeBinding),
            typeof(NetMsmqBinding),
            //typeof(NetPeerTcpBinding),
            typeof(MsmqIntegrationBinding),
            typeof(BasicHttpContextBinding),
            typeof(NetTcpContextBinding),
            typeof(WebHttpBinding),
            typeof(WSHttpContextBinding),
            typeof(UdpBinding)
        };

        static public bool Contains(Type bindingType)
        {
            return _systemBindingTypes.Contains(bindingType);
        }
    }
}
