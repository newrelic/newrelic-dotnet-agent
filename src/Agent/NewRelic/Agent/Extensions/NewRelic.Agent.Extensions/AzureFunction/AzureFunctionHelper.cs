// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.AzureFunction
{
    public static class AzureFunctionHelper
    {
        public static string GetResourceUri(string websiteSiteName)
        {
            var websiteResourceGroup = GetResourceGroupName();
            var subscriptionId = GetSubscriptionId();

            if (string.IsNullOrEmpty(websiteResourceGroup) || string.IsNullOrEmpty(subscriptionId))
            {
                return string.Empty;
            }

            return $"/subscriptions/{subscriptionId}/resourceGroups/{websiteResourceGroup}/providers/Microsoft.Web/sites/{websiteSiteName}";
        }

        public static string GetResourceGroupName()
        {
            // WEBSITE_RESOURCE_GROUP doesn't seem to always be available for Linux.
            var websiteResourceGroup = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            if (!string.IsNullOrEmpty(websiteResourceGroup))
            {
                return websiteResourceGroup; // Must be Windows function
            }

            // The WEBSITE_OWNER_NAME variable also has the resource group name, but we need to parse it out.
            // Must be a Linux function.
            var websiteOwnerName = Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME");
            if (string.IsNullOrEmpty(websiteOwnerName))
            {
                return websiteOwnerName; // This should not happen, but just in case.
            }

            var idx = websiteOwnerName.IndexOf("+", StringComparison.Ordinal);
            if (idx <= 0)
            {
                return websiteOwnerName; // This means that the format of the WEBSITE_OWNER_NAME is not as expected (subscription+resourcegroup-region-Linux).
            }

            // We should have a WEBSITE_OWNER_NAME in the expected format here.
            idx += 1; // move past the "+"
            var resourceData = websiteOwnerName.Substring(idx, websiteOwnerName.Length - idx - 6); // -6 to remove the "-Linux" suffix.

            // Remove the region from the resourceData.
            return resourceData.Substring(0, resourceData.LastIndexOf("-", StringComparison.Ordinal));
        }

        public static string GetSubscriptionId()
        {
            var websiteOwnerName = Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME") ?? string.Empty;
            var idx = websiteOwnerName.IndexOf("+", StringComparison.Ordinal);
            return idx > 0 ? websiteOwnerName.Substring(0, idx) : websiteOwnerName;
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
