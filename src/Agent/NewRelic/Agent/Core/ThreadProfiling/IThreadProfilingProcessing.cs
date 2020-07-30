/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public interface IThreadProfilingProcessing
    {
        ArrayList PruningList { get; }
        void UpdateTree(StackInfo stackInfo, uint depth);
        void AddNodeToPruningList(ProfileNode node);
        void ResetCache();
        void SortPruningTree();
    }
}
