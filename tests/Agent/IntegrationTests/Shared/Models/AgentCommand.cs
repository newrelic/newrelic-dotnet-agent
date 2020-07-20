using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTests.Shared.Models
{
	public class AgentCommand : IEnumerable
	{
		public readonly int CommandId;
		public readonly CommandDetails Details;

		public AgentCommand(int commandId, CommandDetails details)
		{
			CommandId = commandId;
			Details = details;
		}

		/// <summary>
		/// AgentCommand will automatically be serialized as a JSON array because it is IEnumerable.
		/// Members appear in array in yield order.
		/// </summary>
		/// <returns></returns>
		public IEnumerator GetEnumerator()
		{
			yield return CommandId;
			yield return Details;
		}
	}

	public class CommandDetails
	{
		[JsonProperty("name")]
		public readonly string Name;

		[JsonProperty("arguments")]
		public readonly IDictionary<string, object> Arguments;

		public CommandDetails(string name, IDictionary<string, object> arguments)
		{
			Name = name;
			Arguments = arguments;
		}
	}
}
