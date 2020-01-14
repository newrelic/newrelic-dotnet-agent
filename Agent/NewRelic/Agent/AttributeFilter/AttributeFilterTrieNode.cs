using System.Linq;
using NewRelic.Trie;

namespace NewRelic.Agent
{
	internal static class AttributeFilterTrieNode
	{
		public static Clude GetClusion(this TrieNode<AttributeFilterNode> nodeBuilder, IAttribute attribute, AttributeDestinations destination)
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

		private static Clude GetClusionFromChildren(this TrieNode<AttributeFilterNode> nodeBuilder, IAttribute attribute, AttributeDestinations destination)
		{
			var child = nodeBuilder.ApplicableChild(attribute);
			var childClusion = child.GetChildClusion(attribute, destination);
			return childClusion;
		}

		private static Clude GetChildClusion(this TrieNode<AttributeFilterNode> child, IAttribute attribute, AttributeDestinations destination)
		{
			if (child == null)
				return Clude.Unknown;

			return child.GetClusion(attribute, destination);
		}

		private static bool NodeAppliesToAttribute(this TrieNode<AttributeFilterNode> nodeBuilder, IAttribute attribute)
		{
			if (nodeBuilder.Data.Wildcard)
				return attribute.Key.StartsWith(nodeBuilder.Data.Key);
			else
				return attribute.Key == nodeBuilder.Data.Key;
		}

		private static TrieNode<AttributeFilterNode> ApplicableChild(this TrieNode<AttributeFilterNode> nodeBuilder, IAttribute attribute)
		{
			return nodeBuilder.Children
				.Where(child => child != null)
				.Where(child => child.NodeAppliesToAttribute(attribute))
				.FirstOrDefault();
		}
	}
}
