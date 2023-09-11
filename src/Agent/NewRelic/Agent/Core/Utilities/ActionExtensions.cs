// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.Logging;
using System;

namespace NewRelic.Agent.Core.Utilities
{
    public static class ActionExtensions
    {
        public static void CatchAndLog(this Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, $"An exception occurred while doing some background work");
                }
                catch
                {
                }
            }
        }
    }
}
