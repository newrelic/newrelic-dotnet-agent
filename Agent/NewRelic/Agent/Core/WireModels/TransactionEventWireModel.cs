using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Collections;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.WireModels
{
	[JsonConverter(typeof(JsonArrayConverter))]
	[JsonObject(MemberSerialization.OptIn)]
	public class TransactionEventWireModel : IHasPriority
	{
		[JsonArrayIndex(Index = 0)]
		public readonly ReadOnlyDictionary<string, object> IntrinsicAttributes;

		[JsonArrayIndex(Index = 1)]
		public readonly ReadOnlyDictionary<string, object> UserAttributes;

		[JsonArrayIndex(Index = 2)]
		public readonly ReadOnlyDictionary<string, object> AgentAttributes;

		public bool HasOutgoingDistributedTracePayload { get; set; }
		public bool HasIncomingDistributedTracePayload { get; set; }

		private readonly bool _isSynthetics;

		private float _priority;

		public float Priority
		{
			get { return _priority; }
			set
			{
				const float priorityMin = 0.0f;
				if (value < priorityMin || float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
				{
					throw new ArgumentException($"Transaction event requires a valid priority value greater than {priorityMin}, value used: {value}");
				}
				_priority = value;
			}
		}

		public TransactionEventWireModel([NotNull] IEnumerable<KeyValuePair<string, object>> userAttributes, [NotNull] IEnumerable<KeyValuePair<string, object>> agentAttributes, [NotNull] IEnumerable<KeyValuePair<string, object>> intrinsicAttributes, bool isSynthetics, float priority,bool hasOutgoingDistributedTracePayload, bool hasIncomingDistributedTracePayload)
		{
			Priority = priority;
			HasOutgoingDistributedTracePayload = hasOutgoingDistributedTracePayload;
			HasIncomingDistributedTracePayload = hasIncomingDistributedTracePayload;
			IntrinsicAttributes = new ReadOnlyDictionary<string, object>(intrinsicAttributes.ToDictionary<string, object>());
			UserAttributes = new ReadOnlyDictionary<string, object>(userAttributes.ToDictionary<string, object>());
			AgentAttributes = new ReadOnlyDictionary<string, object>(agentAttributes.ToDictionary<string, object>());
			_isSynthetics = isSynthetics;
		}

		public bool IsSynthetics()
		{
			// An event will always contain either all of the synthetics keys or none of them.
			// There is no need to check for the presence of each synthetics key.
			return _isSynthetics;
		}
	}
}
