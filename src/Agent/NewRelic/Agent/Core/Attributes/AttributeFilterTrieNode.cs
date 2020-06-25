/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Linq;

namespace NewRelic.Agent.Core.Attributes
{
    internal static class AttributeFilterTrieNode
    {
        public static Clude GetClusion(this TrieNode<AttributeFilterNode> nodeBuilder, string name, AttributeDestinations destination)
        {
            var childClusion = nodeBuilder.GetClusionFromChildren(name, destination);
            if (childClusion != Clude.Unknown)
                return childClusion;

            var exclude = (nodeBuilder.Data.DestinationExcludes & destination) == destination;
            if (exclude)
                return Clude.Exclude;

            var include = (nodeBuilder.Data.DestinationIncludes & destination) == destination;
            if (include)
                return Clude.Include;

            return Clude.Unknown;
        }

        private static Clude GetClusionFromChildren(this TrieNode<AttributeFilterNode> nodeBuilder, string name, AttributeDestinations destination)
        {
            var child = nodeBuilder.ApplicableChild(name);
            var childClusion = child.GetChildClusion(name, destination);
            return childClusion;
        }

        private static Clude GetChildClusion(this TrieNode<AttributeFilterNode> child, string name, AttributeDestinations destination)
        {
            if (child == null)
                return Clude.Unknown;

            return child.GetClusion(name, destination);
        }

        private static bool NodeAppliesToAttribute(this TrieNode<AttributeFilterNode> nodeBuilder, string name)
        {
            if (nodeBuilder.Data.Wildcard)
                return name.StartsWith(nodeBuilder.Data.Key);
            else
                return name == nodeBuilder.Data.Key;
        }

        private static TrieNode<AttributeFilterNode> ApplicableChild(this TrieNode<AttributeFilterNode> nodeBuilder, string name)
        {
            return nodeBuilder.Children
            .Where(child => child != null)
            .Where(child => child.NodeAppliesToAttribute(name))
            .FirstOrDefault();
        }
    }
}
