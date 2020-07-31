// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent
{
    public interface IAttribute
    {
        string Key { get; }
        object Value { get; }

        AttributeDestinations DefaultDestinations { get; }
    }
}
