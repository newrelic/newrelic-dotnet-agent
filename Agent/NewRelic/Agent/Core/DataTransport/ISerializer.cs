namespace NewRelic.Agent.Core.DataTransport
{
	public interface ISerializer
	{
		string Serialize(object[] parameters);
		T Deserialize<T>(string responseBody);
	}
}
