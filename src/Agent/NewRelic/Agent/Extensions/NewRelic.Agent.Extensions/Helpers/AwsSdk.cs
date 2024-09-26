// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Helpers
{
    public static class AwsSdkHelpers
    {
        public static string ConstructArn(IAgent agent, string invocationName, string region, string accountId)
        {
            if (invocationName.StartsWith("arn:"))
            {
                if (invocationName.StartsWith("arn:aws:lambda:"))
                {
                    return invocationName;
                }
                agent?.Logger.Debug($"Unable to parse function name '{invocationName}'");
                return null;
            }
            var segments = invocationName.Split(':');
            string functionName;

            if ((segments.Length == 1) || (segments.Length == 2))
            {
                // 'myfunction' or 'myfunction:alias'
                // Need account ID to reconstruct ARN
                if (string.IsNullOrEmpty(accountId))
                {
                    agent?.Logger.Debug($"Need account ID in order to resolve function '{invocationName}'");
                    return null;
                }
                functionName = invocationName;
            }
            else if (segments.Length == 3)
            {
                // 123456789012:function:my-function'
                accountId = segments[0];
                functionName = segments[2];
            }
            else if (segments.Length == 4)
            {
                // 123456789012:function:my-function:myalias
                accountId = segments[0];
                functionName = $"{segments[2]}:{segments[3]}";
            }
            else
            {
                agent?.Logger.Debug($"Unable to parse function name '{invocationName}'.");
                return null;
            }
            if (string.IsNullOrEmpty(region))
            {
                agent?.Logger.Debug($"Need region in order to resolve function '{invocationName}'");
                return null;
            }
            return $"arn:aws:lambda:{region}:{accountId}:function:{functionName}";
        }
    }
}
