
namespace NewRelic.Agent.Extensions.Parsing
{
	public static class UriHelpers
	{
		public static string GetTransactionNameFromPath(string path)
		{
			if (path.StartsWith("/"))
				path = path.Substring(1);

			if (path == string.Empty)
				path = "Root";

			return path;
		}
	}
}
