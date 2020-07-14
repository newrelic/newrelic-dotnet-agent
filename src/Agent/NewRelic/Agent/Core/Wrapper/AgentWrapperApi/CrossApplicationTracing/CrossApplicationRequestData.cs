using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
	// Note: this data is referred to as "TransactionData" in the CAT spec.
	[JsonConverter(typeof(JsonArrayConverter)), UsedImplicitly]
	public class CrossApplicationRequestData
	{
		[CanBeNull, JsonArrayIndex(Index = 0), UsedImplicitly]
		public readonly String TransactionGuid;
		[JsonArrayIndex(Index = 1), UsedImplicitly]
		public readonly Boolean Unused;
		[CanBeNull, JsonArrayIndex(Index = 2), UsedImplicitly]
		public readonly String TripId;
		[CanBeNull, JsonArrayIndex(Index = 3), UsedImplicitly]
		public readonly String PathHash;

		// For backwards compatibility we need to support deserializing transactionData that may be missing any number of fields
		public CrossApplicationRequestData()
		{

		}

		public CrossApplicationRequestData(String transactionGuid)
		{
			TransactionGuid = transactionGuid;
		}

		public CrossApplicationRequestData(String transactionGuid, Boolean unused)
		{
			TransactionGuid = transactionGuid;
			Unused = unused;
		}

		public CrossApplicationRequestData(String transactionGuid, Boolean unused, String tripId)
		{
			TransactionGuid = transactionGuid;
			Unused = unused;
			TripId = tripId;
		}

		public CrossApplicationRequestData(String transactionGuid, Boolean unused, String tripId, String pathHash)
		{
			TransactionGuid = transactionGuid;
			Unused = unused;
			TripId = tripId;
			PathHash = pathHash;
		}
	}
}
