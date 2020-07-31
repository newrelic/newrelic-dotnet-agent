// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public static class IAgentWrapperExtensions
    {
        public static void HandleExceptions(this IAgentWrapperApi agentWrapperApi, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                agentWrapperApi.SafeHandleException(ex);
            }
        }

        /// <summary>
        /// Attempts to call agentWrapperApi.HandleWrapperException(ex). Catches and swallows any exceptions
        /// to prevent them from harming instrumented application.
        /// </summary>
        /// <param name="agentWrapperApi"></param>
        /// <param name="ex"></param>
        public static void SafeHandleException(this IAgentWrapperApi agentWrapperApi, Exception ex)
        {
            try
            {
                agentWrapperApi.HandleWrapperException(ex);
            }
            catch
            {
                // Not much more we can do here. Prevent exception from harming instrumented application.
            }
        }
    }
}
