/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.OpenTracing.AmazonLambda.Util
{
    internal static class TagExtensions
    {
        internal static bool IsAgentAttribute(this KeyValuePair<string, object> tag)
        {
            return (tag.Key.StartsWith("aws.")
                    || tag.Key.StartsWith("span.")
                    || tag.Key.StartsWith("peer.")
                    || tag.Key.StartsWith("db.")
                    || tag.Key == "component"
                    || tag.Key == "error"
                    || tag.Key.StartsWith("http.")
                    || tag.Key.StartsWith("request.")
                    || tag.Key.StartsWith("response."));
        }

        internal static IEnumerable<KeyValuePair<string, object>> GetAttributes(this KeyValuePair<string, object> tag)
        {
            if (tag.Value == null)
            {
                return Enumerable.Empty<KeyValuePair<string, object>>();
            }

            if (tag.Key == "http.status_code")
            {
                var attributeList = new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("http.statusCode", tag.Value) };

                var statusCodeString = tag.Value.ToString();
                if (!string.IsNullOrEmpty(statusCodeString))
                {
                    attributeList.Add(new KeyValuePair<string, object>("response.status", statusCodeString));
                }

                return attributeList;
            }

            if (tag.Key == "response.status")
            {
                var statusCodeString = tag.Value.ToString();

                if (string.IsNullOrEmpty(statusCodeString))
                {
                    return Enumerable.Empty<KeyValuePair<string, object>>();
                }

                var attributeList = new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("response.status", statusCodeString) };

                if (int.TryParse(statusCodeString, out var statusCodeInt))
                {
                    attributeList.Add(new KeyValuePair<string, object>("http.statusCode", statusCodeInt));
                }

                return attributeList;
            }

            return new[] { new KeyValuePair<string, object>(tag.Key, tag.Value) };
        }

        internal static IDictionary<string, object> BuildUserAttributes(this IDictionary<string, object> tags)
        {
            var userAttributes = new Dictionary<string, object>();

            if (tags == null)
            {
                return userAttributes;
            }

            foreach (var tag in tags)
            {
                if (!tag.IsAgentAttribute())
                {
                    userAttributes.Add(tag.Key, tag.Value);
                }
            }

            return userAttributes;
        }

        internal static IDictionary<string, object> BuildAgentAttributes(this IDictionary<string, object> tags)
        {
            var agentAttributes = new Dictionary<string, object>();

            if (tags == null)
            {
                return agentAttributes;
            }

            foreach (var tag in tags)
            {
                if (tag.IsAgentAttribute())
                {
                    foreach (var attribute in tag.GetAttributes())
                    {
                        agentAttributes.Add(attribute.Key, attribute.Value);
                    }
                }
            }

            return agentAttributes;
        }
    }
}
