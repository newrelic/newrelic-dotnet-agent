// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Helpers
{
    public static class AwsSdkHelpers
    {
        private static Regex RegionRegex = new Regex(@"^[a-z]{2}((-gov)|(-iso([a-z]?)))?-[a-z]+-\d{1}$", RegexOptions.Compiled);
        private static bool LooksLikeARegion(string text) => RegionRegex.IsMatch(text);
        private static bool LooksLikeAnAccountId(string text) => (text.Length == 12) && text.All(c => c >= '0' && c <= '9');
        // Only log ARNs we can't parse once
        private static HashSet<string> BadInvocations = new HashSet<string>();

        // This is the full regex pattern for an ARN:
        // (arn:(aws[a-zA-Z-]*)?:lambda:)?([a-z]{2}((-gov)|(-iso([a-z]?)))?-[a-z]+-\d{1}:)?(\d{12}:)?(function:)?([a-zA-Z0-9-_\.]+)(:(\$LATEST|[a-zA-Z0-9-_]+))?

        // If it's a full ARN, it has to start with 'arn:'
        // A partial ARN can contain up to 5 segments separated by ':'
        // 1. Region
        // 2. Account ID
        // 3. 'function' (fixed string)
        // 4. Function name
        // 5. Alias or version
        // Only the function name is required, the reset are all optional. e.g. you could have region and function name and nothing else

        // Note that this will not catch functions where the name also looks like a region or account ID
        public static string ConstructArn(IAgent agent, string invocationName, string region, string accountId)
        {
            if (invocationName.StartsWith("arn:"))
            {
                return invocationName;
            }
            var segments = invocationName.Split(':');
            string functionName = null;
            string alias = null;

            foreach (var segment in segments)
            {
                if (LooksLikeARegion(segment) && string.IsNullOrEmpty(region))
                {
                    region = segment;
                }
                else if (LooksLikeAnAccountId(segment) && string.IsNullOrEmpty(accountId))
                {
                    accountId = segment;
                }
                else if (segment == "function")
                {
                    continue;
                }
                else if (functionName == null)
                {
                    functionName = segment;
                }
                else if (alias == null)
                {
                    alias = segment;
                }
                else
                {
                    if (BadInvocations.Add(invocationName))
                    {
                        agent?.Logger.Debug($"Unable to parse function name '{invocationName}'");
                    }
                    return null;
                }
            }

            if (string.IsNullOrEmpty(accountId))
            {
                if (BadInvocations.Add(invocationName))
                {
                    agent?.Logger.Debug($"Need account ID in order to resolve function '{invocationName}'");
                }
                return null;
            }
            if (string.IsNullOrEmpty(region))
            {
                if (BadInvocations.Add(invocationName))
                {
                    agent?.Logger.Debug($"Need region in order to resolve function '{invocationName}'");
                }
                return null;
            }
            if (!string.IsNullOrEmpty(alias))
            {
                return $"arn:aws:lambda:{region}:{accountId}:function:{functionName}:{alias}";
            }
            return $"arn:aws:lambda:{region}:{accountId}:function:{functionName}";
        }
    }
}
