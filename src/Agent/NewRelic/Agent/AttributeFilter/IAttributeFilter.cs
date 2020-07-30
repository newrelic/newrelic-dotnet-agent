/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;

namespace NewRelic.Agent
{
    public interface IAttributeFilter<T> where T : IAttribute
    {
        IEnumerable<T> FilterAttributes(IEnumerable<T> attributes, AttributeDestinations destination);
    }
}
