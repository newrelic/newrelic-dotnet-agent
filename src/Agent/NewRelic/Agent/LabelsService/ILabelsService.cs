/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;

namespace NewRelic.Agent
{
    public interface ILabelsService : IDisposable
    {
        IEnumerable<Label> Labels { get; }
    }
}
