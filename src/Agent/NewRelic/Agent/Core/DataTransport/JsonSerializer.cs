using System;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport
{
	public class JsonSerializer : ISerializer
	{
		public string Serialize(Object[] parameters)
		{
			return JsonConvert.SerializeObject(parameters);
		}

		public T Deserialize<T>(string responseBody)
		{
			return JsonConvert.DeserializeObject<T>(responseBody);
		}
	}
}
