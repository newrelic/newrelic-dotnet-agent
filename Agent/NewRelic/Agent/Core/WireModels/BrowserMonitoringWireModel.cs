using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.WireModels
{
	public class BrowserMonitoringWireModel
	{
		[JsonConverter(typeof(EventAttributesJsonConverter))]
		public readonly IDictionary<string, object> a;

		[JsonConverter(typeof(EventAttributesJsonConverter))]
		public readonly IDictionary<string, object> u;

		public BrowserMonitoringWireModel(IDictionary<string, object> agentAttributes, IDictionary<string, object> userAttributes)
		{
			if (agentAttributes != null && agentAttributes.Count > 0)
			{
				a = agentAttributes;
			}

			if (userAttributes != null && userAttributes.Count() > 0)
			{
				u = userAttributes;
			}
		}

	}
}