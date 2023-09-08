// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Time
{
    public interface ISimpleTimerFactory
    {
        /// <summary>
        /// Starts and returns a new timer.
        /// </summary>
        /// <returns>A started timer.</returns>
        ISimpleTimer StartNewTimer();
    }

    public class SimpleTimerFactory : ISimpleTimerFactory
    {
        public ISimpleTimer StartNewTimer()
        {
            return new SimpleTimer();
        }
    }
}
