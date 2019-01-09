using System;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using NewRelic.Agent.Core.Logging;

namespace NewRelic.Agent.Core.DistributedTracing
{
	/// <remarks>
	/// https://source.datanerd.us/agents/agent-specs/blob/master/Distributed-Tracing.md#payload-fields
	/// </remarks>
	[JsonConverter(typeof(DistributedTracePayloadJsonConverter))]
	public class DistributedTracePayload
	{
		public const int SupportedMajorVersion = 0;
		public const int SupportedMinorVersion = 1;

		///<summary>Version [major, minor]</summary>
		public int[] Version { get; set; } = { SupportedMajorVersion, SupportedMinorVersion };

		/// <summary>
		/// This field contains either: "App", "Browser", "Mobile." 
		/// Prevents ambiguity if different application types share account/app numbers.
		/// </summary>
		public string Type { get; set; }

		///<summary>The APM account identifier</summary>
		public string AccountId { get; set; }

		///<summary>The application identifier (i.e. cluster agent ID)</summary>
		public string AppId { get; set; }

		///<summary>Current span (transaction, request, etc.) identifier</summary>
		public string Guid { get; set; }

		///<summary>Links all spans within the call chain together</summary>
		public string TraceId { get; set; }

		/// <summary> The trusted account key received from the connect response. </summary>
		public string TrustKey { get; set; }
	
		///<summary>Likelihood to be saved</summary>
		public float? Priority { get; set; }

		///<summary>Whether this trip should be sampled</summary>
		public bool? Sampled { get; set; }

		///<summary>Unix timestamp in milliseconds when the payload was created</summary>
		public DateTime Timestamp { get; set; }

		/// <summary>The transaction guid (when applicable)</summary>
		public string TransactionId { get; set; }

		// This public default constructor is required for our JSON deserialization code.
		public DistributedTracePayload()
		{
		}

		private DistributedTracePayload(string type, string accountId, string appId, string guid,
			string traceId, string trustKey, float? priority, bool? sampled, DateTime timestamp,
			string transactionId)
		{
			Type = type;
			AccountId = accountId;
			AppId = appId;
			Guid = guid;
			TraceId = traceId;
			TrustKey = trustKey;
			Priority = priority;
			Sampled = sampled;
			Timestamp = timestamp;
			TransactionId = transactionId;
		}

		public static DistributedTracePayload TryBuildOutgoingPayload(string type, string accountId, string appId, string guid,
				string traceId, string trustKey, float? priority, bool? sampled, DateTime timestamp,
				string transactionId)
		{

			if ( string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(appId))
			{ 
				Log.Finest("Did not generate payload because AccountId or PrimaryApplicationId were not populated. This is normal for requests occurring before round trip with server has completed.");
				return null;
			}

			if ( string.IsNullOrEmpty(type) || string.IsNullOrEmpty(traceId))
			{
				Log.Finest("Did not generate payload becasue Type or TraceId were not populated.");
				return null;
			}

			if (string.IsNullOrEmpty(guid) && string.IsNullOrEmpty(transactionId))
			{
				Log.Finest("Did not generate payload because neither guid nor transactionId were populated.");
				return null;
			}

			if (accountId == trustKey)
			{
				trustKey = null;
			}

			return new DistributedTracePayload(type, accountId, appId, guid, traceId, trustKey, priority, sampled, timestamp, transactionId);
		}

		/// <summary>
		/// Deserialize a JSON string into a DistributedTracePayload.
		/// </summary>
		/// <param name="json">A JSON string representing a DistributedTracePayload</param>
		/// <returns>A DistributedTracePayload</returns>
		public static DistributedTracePayload TryBuildIncomingPayloadFromJson(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				throw new DistributedTraceAcceptPayloadNullException("json input cannot be null or empty string");
			}

			try
			{
				return JsonConvert.DeserializeObject<DistributedTracePayload>(json);
			}
			catch (JsonException e)
			{
				throw new DistributedTraceAcceptPayloadParseException("trouble parsing json - see inner exception for details",
					e);
			}
		}

		/// <summary>
		/// Serialize a DistributedTracePayload <paramref name="payload"/> to an, optionally pretty, JSON string.
		/// </summary>
		/// <param name="payload">The DistributedTracePayload</param>
		/// <param name="pretty">When true, the JSON string will have extra whitespace/new lines. When false, the JSON will be compact.</param>
		/// <returns>The serialized JSON string</returns>
		public static string ToJson(DistributedTracePayload payload, bool pretty = false)
		{
			return JsonConvert.SerializeObject(payload, pretty ? Formatting.Indented: Formatting.None);
		}
	}
}
