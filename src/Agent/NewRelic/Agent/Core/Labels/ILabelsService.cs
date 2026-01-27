// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Labels;

public interface ILabelsService : IDisposable
{
    IEnumerable<Label> Labels { get; }

    IEnumerable<Label> GetFilteredLabels(IEnumerable<string> labelsToExclude);
}
