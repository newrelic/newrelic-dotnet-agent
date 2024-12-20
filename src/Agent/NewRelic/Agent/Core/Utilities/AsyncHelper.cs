// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.Utilities
{
    // Borrowed from https://stackoverflow.com/a/56928748/2078975
    [NrExcludeFromCodeCoverage]
    public class AsyncHelper
    {
        private static readonly TaskFactory _taskFactory =
            new(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

        /// <summary>
        /// Safely executes an async method synchronously.
        /// </summary>
        /// <typeparam name="TReturn"></typeparam>
        /// <param name="task"></param>
        /// <returns></returns>
        public static TReturn RunSync<TReturn>(Func<Task<TReturn>> task)
        {
            return _taskFactory.StartNew(task)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        }
    }
}
#endif
