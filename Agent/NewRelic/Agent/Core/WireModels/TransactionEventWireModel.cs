using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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

		public bool HasOutgoingDistributedTracePayload { get; set; }
		public bool HasIncomingDistributedTracePayload { get; set; }

		private readonly bool _isSynthetics;

		private const string ItemTypeName = "Transaction Event";
		private const string TimeStampKey = "timestamp";
		private const float PriorityMin = 0.0f;
		private static readonly string _missingTimestampMessage = $"{ItemTypeName} does not contain '{TimeStampKey}'";
		private float _priority;

		public float Priority
		{
			get { return _priority; }
			set
			{
				if (value < PriorityMin || float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
				{
					throw new ArgumentException($"{ItemTypeName} requires a valid priority value greater than {PriorityMin}, value used: {value}");
				}
				_priority = value;
			}
		}

		//Compares on Priority (descending 1.0 -> 0.0), if priority is equal, compares timestamp values (ascending double values)
		//if priorities are equal the event that has the earliest timestamp will sort higher.
		public class PriorityTimestampComparer : Comparer<TransactionEventWireModel>
		{
			public override int Compare(TransactionEventWireModel x, TransactionEventWireModel y)
			{
				//null cases: null < something, something > null, null == null
				if (x == null)
				{
					return (y == null) ? 0 : -1;
				}
				if (y == null)
				{
					return 1;
				}

				//descending by Priority, if equal resort to timestamp(ascending) to break the tie.
				//larger priority values are sorted to be first
				var priorityComparison = x.Priority.CompareTo(y.Priority);
				if (0 != priorityComparison)
				{
					//negate to achieve descending...
					return -priorityComparison;
				}

				//priorities are equal, fetch the timestamps;
				if (!x.IntrinsicAttributes.ToDictionary().TryGetValue(TimeStampKey, out object xTimestampValue))
				{
					throw new ArgumentException(_missingTimestampMessage, nameof(x));
				}

				if (!y.IntrinsicAttributes.ToDictionary().TryGetValue(TimeStampKey, out object yTimestampValue))
				{
					throw new ArgumentException(_missingTimestampMessage, nameof(y));
				}

				var xTimestamp = (double)xTimestampValue;
				var yTimestamp = (double)yTimestampValue;
				return xTimestamp.CompareTo(yTimestamp);
			}
		}

		public TransactionEventWireModel([NotNull] IEnumerable<KeyValuePair<String, Object>> userAttributes, [NotNull] IEnumerable<KeyValuePair<String, Object>> agentAttributes, [NotNull] IEnumerable<KeyValuePair<String, Object>> intrinsicAttributes, bool isSynthetics, float priority,bool hasOutgoingDistributedTracePayload, bool hasIncomingDistributedTracePayload)
		{
			Priority = priority;
			HasOutgoingDistributedTracePayload = hasOutgoingDistributedTracePayload;
			HasIncomingDistributedTracePayload = hasIncomingDistributedTracePayload;
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
