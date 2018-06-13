using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using DistributedTracePayload = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing.DistributedTracePayload;

namespace NewRelic.Agent.Core.JsonConverters
{

	public class DistributedTracePayloadJsonConverter : JsonConverter
	{

		private static void ParseVersion(JToken selection, DistributedTracePayload payload)
		{
			payload.Version = selection.ToObject<int[]>();
			var payloadMajorVersion = payload.Version[0];
			var payloadMinorVersion = payload.Version[1];
			if (payloadMajorVersion != DistributedTracePayload.SupportedMajorVersion || payloadMinorVersion != DistributedTracePayload.SupportedMinorVersion)
			{
				throw new JsonException(
					$"unsupported DistributedTracePayload version. Expected: {DistributedTracePayload.SupportedMajorVersion}.{DistributedTracePayload.SupportedMinorVersion}  Found: {payloadMajorVersion}.{payloadMinorVersion}");
			}
		}

		private readonly ValidationConstraint<DistributedTracePayload>[] _validationConstraints = new[]
		{
			new ValidationConstraint<DistributedTracePayload>("v", JTokenType.Array, true, 2, 2, ParseVersion),
			new ValidationConstraint<DistributedTracePayload>("d", JTokenType.Object, true, 7, 9, null),
			new ValidationConstraint<DistributedTracePayload>("d.ty", JTokenType.String, true, 0, 0, (s,p) => p.Type = s.ToObject<string>()),
			new ValidationConstraint<DistributedTracePayload>("d.ac", JTokenType.String, true, 0, 0, (s,p) => p.Account = s.ToObject<string>()),
			new ValidationConstraint<DistributedTracePayload>("d.ap", JTokenType.String, true, 0, 0, (s,p) => p.App = s.ToObject<string>()),
			new ValidationConstraint<DistributedTracePayload>("d.pa", JTokenType.String, false, 0, 0, (s,p) => p.ParentId = s.ToObject<string>()),
			new ValidationConstraint<DistributedTracePayload>("d.id", JTokenType.String, true, 0, 0, (s,p) => p.Guid = s.ToObject<string>()),
			new ValidationConstraint<DistributedTracePayload>("d.tr", JTokenType.String, true, 0, 0, (s,p) => p.TraceId = s.ToObject<string>()),
			new ValidationConstraint<DistributedTracePayload>("d.pr", JTokenType.Float, false, 0, 0, (s,p) => p.Priority = s.ToObject<float>()),
			new ValidationConstraint<DistributedTracePayload>("d.sa", JTokenType.Boolean, false, 0, 0, (s,p) => p.Sampled = s.ToObject<bool>()),
			new ValidationConstraint<DistributedTracePayload>("d.ti", JTokenType.Integer, true, 0, 0, (s,p) => p.Time = s.ToObject<long>().FromUnixTimeMilliseconds()),
		};

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (value is DistributedTracePayload payload)
			{
				var serializedPayload = new JObject(
					new JProperty("v", new JArray(payload.Version)),
					new JProperty("d", new JObject(
						new JProperty("ty", payload.Type),
						new JProperty("ac", payload.Account),
						new JProperty("ap", payload.App),
						new JProperty("pa", payload.ParentId),
						new JProperty("id", payload.Guid),
						new JProperty("tr", payload.TraceId),
						new JProperty("pr", payload.Priority),
						new JProperty("sa", payload.Sampled),
						new JProperty("ti", payload.Time.ToUnixTimeMilliseconds()))
					));
				serializedPayload.WriteTo(writer);
				return;
			}
			throw new ArgumentException("invalid object type passed to " + nameof(DistributedTracePayloadJsonConverter), nameof(value));
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			//reader should be positioned at the start of the object "{"
			if (reader.TokenType != JsonToken.StartObject)
			{
				throw new JsonException(
					$"expected to find beginning of DistributedTracePayload object. Found token: {Enum.GetName(typeof(JsonToken), reader.TokenType)}");
			}

			var parsedPayload = new DistributedTracePayload();
			var jObject = JObject.Load(reader);
			foreach (var constraint in _validationConstraints)
			{
				constraint.ParseAndThrowOnFailure(jObject, parsedPayload);
			}
			return parsedPayload;
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(DistributedTracePayload);
		}
	}
}
	
