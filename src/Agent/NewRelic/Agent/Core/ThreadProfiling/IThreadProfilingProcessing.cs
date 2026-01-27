// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace NewRelic.Agent.Core.ThreadProfiling;

public interface IThreadProfilingProcessing
{
    ArrayList PruningList { get; }
    void AddNodeToPruningList(ProfileNode node);
    void ResetCache();
    void SortPruningTree();
}