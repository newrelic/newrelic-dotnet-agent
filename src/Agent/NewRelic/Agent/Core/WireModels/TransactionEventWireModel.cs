using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
	[JsonConverter(typeof(JsonArrayConverter))]
	[JsonObject(MemberSerialization.OptIn)]
	public class TransactionEventWireModel
	{
		[JsonArrayIndex(Index = 0)]
		[NotNull]
		public readonly ReadOnlyDictionary<String, Object> IntrinsicAttributes;

		[JsonArrayIndex(Index = 1)]
		[NotNull]
		public readonly ReadOnlyDictionary<String, Object> UserAttributes;

		[JsonArrayIndex(Index = 2)]
		[NotNull]
		public readonly ReadOnlyDictionary<String, Object> AgentAttributes;

		private readonly bool _isSynthetics;

		public TransactionEventWireModel([NotNull] IEnumerable<KeyValuePair<String, Object>> userAttributes, [NotNull] IEnumerable<KeyValuePair<String, Object>> agentAttributes, [NotNull] IEnumerable<KeyValuePair<String, Object>> intrinsicAttributes, bool isSynthetics)
		{
			IntrinsicAttributes = new ReadOnlyDictionary<String, Object>(intrinsicAttributes.ToDictionary<String, Object>());
			UserAttributes = new ReadOnlyDictionary<String, Object>(userAttributes.ToDictionary<String, Object>());
			AgentAttributes = new ReadOnlyDictionary<String, Object>(agentAttributes.ToDictionary<String, Object>());
			_isSynthetics = isSynthetics;
		}

		public Boolean IsSynthetics()
		{
			// An event will always contain either all of the synthetics keys or none of them.
			// There is no need to check for the presence of each synthetics key.
			return _isSynthetics;
		}
	}
}
