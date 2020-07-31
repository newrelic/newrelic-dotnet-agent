// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Requests
{
    public class FilterAttributesRequest
    {
        public readonly AttributeDestinations AttributeDestination;
        public readonly Attributes Attributes;

        public FilterAttributesRequest(Attributes attributes, AttributeDestinations attributeDestination)
        {
            AttributeDestination = attributeDestination;
            Attributes = attributes;
        }
    }
}
