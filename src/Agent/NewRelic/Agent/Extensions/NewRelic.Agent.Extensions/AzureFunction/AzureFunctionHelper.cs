// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.AzureFunction
{
    public static class AzureFunctionHelper
    {
        public static string GetResourceUri(string websiteSiteName)
        {
            // TODO: WEBSITE_RESOURCE_GROUP doesn't seem to be available. 
            string websiteResourceGroup = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP") ?? "unknown";
            string websiteOwnerName = Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME") ?? string.Empty;
            int idx = websiteOwnerName.IndexOf("+", StringComparison.Ordinal);
            string subscriptionId = idx > 0 ? websiteOwnerName.Substring(0, idx) : websiteOwnerName;

            if (string.IsNullOrEmpty(websiteResourceGroup) || string.IsNullOrEmpty(subscriptionId))
            {
                return string.Empty;
            }

            return $"/subscriptions/{subscriptionId}/resourceGroups/{websiteResourceGroup}/providers/Microsoft.Web/sites/{websiteSiteName}";
        }

        public static string GetResourceId()
        {
            return GetResourceUri(GetServiceName());
        }

        public static string GetResourceIdWithFunctionName(string functionName)
        {
            return $"{GetResourceId()}/functions/{functionName}";
        }

        public static string GetRegion()
        {
            return Environment.GetEnvironmentVariable("REGION_NAME");
        }

        public static string GetServiceName()
        {
            return Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
        }

    }
}
