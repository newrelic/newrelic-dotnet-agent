namespace ArtifactBuilder
{
	public class NugetPushInfo
	{
		public NugetPushInfo(string serverUri, string apiKey)
		{
			ServerUri = serverUri;
			ApiKey = apiKey;
		}

		public string ServerUri { get; }
		public string ApiKey { get; }
	}
}
