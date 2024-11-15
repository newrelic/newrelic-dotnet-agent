// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text.RegularExpressions;
using NewRelic.Agent.Extensions.Collections;

namespace NewRelic.Agent.Extensions.AwsSdk
{
    public class ArnBuilder
    {
        public readonly string Partition;
        public readonly string Region;
        public readonly string AccountId;

        public ArnBuilder(string partition, string region, string accountId)
        {
            Partition = partition;
            Region = region;
            AccountId = accountId;
        }

        public string Build(string service, string resource) => ConstructArn(Partition, service, Region, AccountId, resource);

        // This is the full regex pattern for a Lambda ARN:
        // (arn:(aws[a-zA-Z-]*)?:lambda:)?([a-z]{2}((-gov)|(-iso([a-z]?)))?-[a-z]+-\d{1}:)?(\d{12}:)?(function:)?([a-zA-Z0-9-_\.]+)(:(\$LATEST|[a-zA-Z0-9-_]+))?

        // If it's a full ARN, it has to start with 'arn:'
        // A partial ARN can contain up to 5 segments separated by ':'
        // 1. Region
        // 2. Account ID
        // 3. 'function' (fixed string)
        // 4. Function name
        // 5. Alias or version
        // Only the function name is required, the rest are all optional. e.g. you could have region and function name and nothing else
        public string BuildFromPartialLambdaArn(string invocationName)
        {
            if (invocationName.StartsWith("arn:"))
            {
                return invocationName;
            }
            var segments = invocationName.Split(':');
            string functionName = null;
            string alias = null;
            string fallback = null;
            string region = null;
            string accountId = null;

            // If there's only one string, assume it's the function name
            if (segments.Length == 1)
            {
                functionName = segments[0];
            }
            else
            {
                // All we should need is the function name, but if we find a region or account ID, we'll use it
                // since it should be more accurate
                foreach (var segment in segments)
                {
                    // A string that looks like a region or account ID could also be the function name
                    // Assume it's the former, unless we never find a function name
                    if (LooksLikeARegion(segment))
                    {
                        if (string.IsNullOrEmpty(region))
                        {
                            region = segment;
                        }
                        else
                        {
                            fallback = segment;
                        }
                        continue;
                    }
                    else if (LooksLikeAnAccountId(segment))
                    {
                        if (string.IsNullOrEmpty(accountId))
                        {
                            accountId = segment;
                        }
                        else
                        {
                            fallback = segment;
                        }
                        continue;
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
                        return null;
                    }
                }
            }

            if (string.IsNullOrEmpty(functionName))
            {
                if (!string.IsNullOrEmpty(fallback))
                {
                    functionName = fallback;
                }
                else
                {
                    return null;
                }
            }

            accountId = !string.IsNullOrEmpty(accountId) ? accountId : AccountId;
            if (string.IsNullOrEmpty(accountId))
            {
                return null;
            }

            region = !string.IsNullOrEmpty(region) ? region : Region;
            if (string.IsNullOrEmpty(region))
            {
                return null;
            }


            if (!string.IsNullOrEmpty(alias))
            {
                return ConstructArn(Partition, "lambda", region, accountId, $"function:{functionName}:{alias}");

            }
            return ConstructArn(Partition, "lambda", region, accountId, $"function:{functionName}");
        }

        private static Regex RegionRegex = new Regex(@"^[a-z]{2}((-gov)|(-iso([a-z]?)))?-[a-z]+-\d{1}$", RegexOptions.Compiled);
        private static bool LooksLikeARegion(string text) => RegionRegex.IsMatch(text);
        private static bool LooksLikeAnAccountId(string text) => (text.Length == 12) && text.All(c => c >= '0' && c <= '9');

        private string ConstructArn(string partition, string service, string region, string accountId, string resource)
        {
            if (string.IsNullOrEmpty(partition) || string.IsNullOrEmpty(region) || string.IsNullOrEmpty(accountId)
                || string.IsNullOrEmpty(service) || string.IsNullOrEmpty(resource))
            {
                return null;
            }
            return "arn:" + partition + ":" + service + ":" + region + ":" + accountId + ":" + resource;
        }

    }
}
