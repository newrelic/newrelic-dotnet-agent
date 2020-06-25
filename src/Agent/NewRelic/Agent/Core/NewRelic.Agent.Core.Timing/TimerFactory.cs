/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Core.Timing
{
    public interface ITimerFactory
    {
        /// <summary>
        /// Starts and returns a new timer.
        /// </summary>
        /// <returns>A started timer.</returns>
        ITimer StartNewTimer();
    }

    public class TimerFactory : ITimerFactory
    {
        public ITimer StartNewTimer()
        {
            return new Timer();
        }
    }
}
