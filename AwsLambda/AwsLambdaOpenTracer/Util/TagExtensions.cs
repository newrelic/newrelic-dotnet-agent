using System.Collections.Generic;

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

		internal static string GetAttributeName(this KeyValuePair<string, object> tag)
		{
			return tag.Key == "http.status_code" ? "response.status" : tag.Key;
		}
	}
}
