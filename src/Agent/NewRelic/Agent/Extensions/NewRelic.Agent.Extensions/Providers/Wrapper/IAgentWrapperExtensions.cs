// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using System;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public static class IAgentWrapperExtensions
    {
        public static void HandleExceptions(this IAgent agent, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                agent.SafeHandleException(ex);
            }
        }

        /// <summary>
        /// Attempts to call agent.HandleWrapperException(ex). Catches and swallows any exceptions
        /// to prevent them from harming instrumented application.
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="ex"></param>
        public static void SafeHandleException(this IAgent agent, Exception ex)
        {
            try
            {
                agent.HandleWrapperException(ex);
            }
            catch
            {
                // Not much more we can do here. Prevent exception from harming instrumented application.
            }
        }
    }
}
