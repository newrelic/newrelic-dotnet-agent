using System;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Trie;

namespace NewRelic.Agent
{
    internal static class AttributeFilterTrieNode
    {
        public static Clude GetClusion([NotNull] this TrieNode<AttributeFilterNode> nodeBuilder, [NotNull] IAttribute attribute, AttributeDestinations destination)
        {
            var childClusion = nodeBuilder.GetClusionFromChildren(attribute, destination);
            if (childClusion != Clude.Unknown)
                return childClusion;

            var exclude = ((nodeBuilder.Data.DestinationExcludes & destination) == destination);
            if (exclude)
                return Clude.Exclude;

            var include = ((nodeBuilder.Data.DestinationIncludes & destination) == destination);
            if (include)
                return Clude.Include;

            return Clude.Unknown;
        }

        private static Clude GetClusionFromChildren([NotNull] this TrieNode<AttributeFilterNode> nodeBuilder, [NotNull] IAttribute attribute, AttributeDestinations destination)
        {
            var child = nodeBuilder.ApplicableChild(attribute);
            var childClusion = child.GetChildClusion(attribute, destination);
            return childClusion;
        }

        private static Clude GetChildClusion([CanBeNull] this TrieNode<AttributeFilterNode> child, [NotNull] IAttribute attribute, AttributeDestinations destination)
        {
            if (child == null)
                return Clude.Unknown;

            return child.GetClusion(attribute, destination);
        }

        private static Boolean NodeAppliesToAttribute([NotNull] this TrieNode<AttributeFilterNode> nodeBuilder, [NotNull] IAttribute attribute)
        {
            if (nodeBuilder.Data.Wildcard)
                return attribute.Key.StartsWith(nodeBuilder.Data.Key);
            else
                return attribute.Key == nodeBuilder.Data.Key;
        }

        [CanBeNull]
        private static TrieNode<AttributeFilterNode> ApplicableChild([NotNull] this TrieNode<AttributeFilterNode> nodeBuilder, [NotNull] IAttribute attribute)
        {
            return nodeBuilder.Children
                .Where(child => child != null)
                .Where(child => child.NodeAppliesToAttribute(attribute))
                .FirstOrDefault();
        }
    }
}
