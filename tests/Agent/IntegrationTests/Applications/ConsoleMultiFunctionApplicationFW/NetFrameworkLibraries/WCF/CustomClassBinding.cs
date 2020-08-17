// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.ServiceModel;
using System.ServiceModel.Channels;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF
{
    public class CustomClassBinding : Binding
    {
        public override string Scheme => "http";

        public override BindingElementCollection CreateBindingElements()
        {
            var collection = new BindingElementCollection();
            var httpTransport = new HttpTransportBindingElement
            {
                AuthenticationScheme = System.Net.AuthenticationSchemes.Anonymous,
                HostNameComparisonMode = HostNameComparisonMode.StrongWildcard
            };

            collection.Add(httpTransport);
            return collection;
        }
    }
}
