// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace NewRelic.Agent.Core.JsonConverters
{
    public class DistributedTracePayloadJsonConverter : JsonConverter
    {
        private const int RequiredDataObjectFieldCount = 6;
        private const int MaximumDataObjectFieldCount = 10;

        private static void ParseVersion(JToken selection, DistributedTracePayload payload)
        {
            payload.Version = selection.ToObject<int[]>();
            var payloadMajorVersion = payload.Version[0];
            var payloadMinorVersion = payload.Version[1];
            if (payloadMajorVersion > DistributedTracePayload.SupportedMajorVersion)
            {
                throw new DistributedTraceAcceptPayloadVersionException(
                    $"unsupported DistributedTracePayload version. Expected: {DistributedTracePayload.SupportedMajorVersion}.{DistributedTracePayload.SupportedMinorVersion}  Found: {payloadMajorVersion}.{payloadMinorVersion}");
            }
        }

        private static DateTime ParseTimestamp(JToken s)
        {
            var timestampLong = s.ToObject<long>();
            if (timestampLong == 0)
            {
                throw new DistributedTraceAcceptPayloadParseException("expected valid timestamp  Found: invalid timestamp");
            }
            return timestampLong.FromUnixTimeMilliseconds();
        }

        private readonly ValidationConstraint<DistributedTracePayload>[] _validationConstraints = new[]
        {
            new ValidationConstraint<DistributedTracePayload>("v", JTokenType.Array, true, 2, 2, ParseVersion),
            new ValidationConstraint<DistributedTracePayload>("d", JTokenType.Object, true, RequiredDataObjectFieldCount, MaximumDataObjectFieldCount, null),
            new ValidationConstraint<DistributedTracePayload>("d.ty", JTokenType.String, true, 0, 0, (s,p) => p.Type = s.ToObject<string>()),
            new ValidationConstraint<DistributedTracePayload>("d.ac", JTokenType.String, true, 0, 0, (s,p) => p.AccountId = s.ToObject<string>()),
            new ValidationConstraint<DistributedTracePayload>("d.ap", JTokenType.String, true, 0, 0, (s,p) => p.AppId = s.ToObject<string>()),
            new ValidationConstraint<DistributedTracePayload>("d.id", JTokenType.String, false, 0, 0, (s,p) => p.Guid = s.ToObject<string>()),
            new ValidationConstraint<DistributedTracePayload>("d.tr", JTokenType.String, true, 0, 0, (s,p) => p.TraceId = s.ToObject<string>()),
            new ValidationConstraint<DistributedTracePayload>("d.tk", JTokenType.String, false, 0, 0, (s,p) => p.TrustKey = s.ToObject<string>()),
            new ValidationConstraint<DistributedTracePayload>("d.pr", JTokenType.Float, false, 0, 0, (s,p) => p.Priority = s.ToObject<float>()),
            new ValidationConstraint<DistributedTracePayload>("d.sa", JTokenType.Boolean, false, 0, 0, (s,p) => p.Sampled = s.ToObject<bool>()),
            new ValidationConstraint<DistributedTracePayload>("d.ti", JTokenType.Integer, true, 0, 0, (s,p) => p.Timestamp = ParseTimestamp(s)),
            new ValidationConstraint<DistributedTracePayload>("d.tx", JTokenType.String, false, 0, 0, (s,p) => p.TransactionId = s.ToObject<string>()),
        };

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is DistributedTracePayload payload)
            {
                var data = new JObject(
                    new JProperty("ty", payload.Type),
                    new JProperty("ac", payload.AccountId),
                    new JProperty("ap", payload.AppId),
                    new JProperty("tr", payload.TraceId),
                    new JProperty("pr", payload.Priority), //required by agent/outgoing not incoming/accept
                    new JProperty("sa", payload.Sampled), //required by agent/outgoing not incoming/accept
                    new JProperty("ti", payload.Timestamp.ToUnixTimeMilliseconds()));

                if (!string.IsNullOrWhiteSpace(payload.TrustKey))
                {
                    data.Add(new JProperty("tk", payload.TrustKey));
                }

                if (!string.IsNullOrWhiteSpace(payload.TransactionId))
                {
                    data.Add(new JProperty("tx", payload.TransactionId));
                }

                if (!string.IsNullOrWhiteSpace(payload.Guid))
                {
                    data.Add(new JProperty("id", payload.Guid));
                }

                var serializedPayload = new JObject(
                    new JProperty("v", new JArray(payload.Version)),
                    new JProperty("d", data));

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
                throw new DistributedTraceAcceptPayloadParseException(
                    $"expected to find beginning of DistributedTracePayload object. Found token: {Enum.GetName(typeof(JsonToken), reader.TokenType)}");
            }

            var parsedPayload = new DistributedTracePayload();
            var jObject = JObject.Load(reader);
            foreach (var constraint in _validationConstraints)
            {
                constraint.ParseAndThrowOnFailure(jObject, parsedPayload);
            }

            if (string.IsNullOrEmpty(parsedPayload.Guid) && string.IsNullOrEmpty(parsedPayload.TransactionId))
            {
                throw new DistributedTraceAcceptPayloadParseException(
                    $"expected either the Guid or the TransactionId to be present. Found: neither");
            }

            return parsedPayload;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DistributedTracePayload);
        }
    }
}

