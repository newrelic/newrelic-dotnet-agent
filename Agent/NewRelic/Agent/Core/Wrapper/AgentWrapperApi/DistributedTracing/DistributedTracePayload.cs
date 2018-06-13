using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using System;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing
{



	/// <remarks>
	/// https://source.datanerd.us/earnold/agent-specs/blob/dd291549d8af103d64ee6fba061a3c19cd23cc70/Distributed-Tracing.md#payload-fields
	/// </remarks>
	public class DistributedTracePayload
	{
		public const int SupportedMajorVersion = 0;
		public const int SupportedMinorVersion = 1;
		///<summary>Version [major, minor]</summary>
		public int[] Version;

		public string Type;

		///<summary>The APM account identifier</summary>
		public string Account;

		///<summary>The application identifier (i.e. cluster agent ID)</summary>
		public string App;

		///<summary>The caller's span ID</summary>
		public string ParentId;

		///<summary>Current span (transaction, request, etc.) identifier</summary>
		public string Guid;

		///<summary>Links all spans within the call chain together</summary>
		public string TraceId;

		///<summary>Likelihood to be saved</summary>
		public float Priority;

		///<summary>Whether this trip should be sampled</summary>
		public bool Sampled;

		///<summary>Unix timestamp in milliseconds when the payload was created</summary>
		public DateTime Time;

		public DistributedTracePayload() : this(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, -1.0f, false, DateTime.MinValue)
		{

		}

		public DistributedTracePayload(string type, string account, string app, string parentId, string guid,
			string traceId, float priority, bool sampled, DateTime time)
		{
			Version = new[] { SupportedMajorVersion, SupportedMinorVersion };
			Type = type;
			Account = account;
			App = app;
			ParentId = parentId;
			Guid = guid;
			TraceId = traceId;
			Priority = priority;
			Sampled = sampled;
			Time = time;
		}

		/// <summary>
		/// Serialize a DistributedTracePayload <paramref name="payload"/> to an, optionally pretty, JSON string.
		/// </summary>
		/// <param name="payload">The DistributedTracePayload</param>
		/// <param name="pretty">When true, the JSON string will have extra whitespace/new lines. When false, the JSON will be compact.</param>
		/// <returns>The serialized JSON string</returns>
		public static string ToJson(DistributedTracePayload payload, bool pretty = false)
		{
			return JsonConvert.SerializeObject(payload, pretty ? Formatting.Indented: Formatting.None, new DistributedTracePayloadJsonConverter());
		}

		/// <summary>
		/// Deserialize a JSON string into a DistributedTracePayload.
		/// </summary>
		/// <param name="json">A JSON string representing a DistributedTracePayload</param>
		/// <returns>A DistributedTracePayload</returns>
		public static DistributedTracePayload FromJson(string json)
		{
			return JsonConvert.DeserializeObject<DistributedTracePayload>(json, new DistributedTracePayloadJsonConverter());
		}
	}
}
