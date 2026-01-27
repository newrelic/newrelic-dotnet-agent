// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace NewRelic.Agent.Core.ThreadProfiling;

public class ProfileNodeComparer : IComparer
{
    public int Compare(object x, object y)
    {
        int result = 0;

        ProfileNode left = x as ProfileNode;
        ProfileNode right = y as ProfileNode;

        if (left.RunnableCount < right.RunnableCount)
        {
            result = 1;
        }
        else if (left.RunnableCount > right.RunnableCount)
        {
            result = -1;
        }
        else
        {
            if (left.Depth > right.Depth)
            {
                result = 1;
            }
            else if (left.Depth < right.Depth)
            {
                result = -1;
            }
        }

        return result;
    }
}