// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(TransactionTraceConverter))]
    public class TransactionTrace
    {
        // index 0
        public readonly DateTime Timestamp;

        // index 1
        public readonly object Unknown1;

        // index 2
        public readonly object Unknown2;

        //index 3
        public readonly TransactionTraceSegment RootSegment;

        // index 4
        public readonly TransactionTraceAttributes Attributes;

        public TransactionTrace(DateTime timestamp, object unknown1, object unknown2, TransactionTraceSegment rootSegment, TransactionTraceAttributes attributes)
        {
            Timestamp = timestamp;
            Unknown1 = unknown1;
            RootSegment = rootSegment;
            Unknown2 = unknown2;
            Attributes = attributes;
        }

        public class TransactionTraceConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return true;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var jArray = JArray.Load(reader);
                if (jArray == null)
                    throw new JsonSerializationException("Unable to create a jObject from reader.");

                var timestamp = new DateTime(1970, 01, 01) + TimeSpan.FromMilliseconds((double)(jArray[0] ?? 0));
                var unknown1 = jArray[1];
                var unknown2 = jArray[2];
                var rootSegment = (jArray[3] ?? new JObject()).ToObject<TransactionTraceSegment>(serializer);
                var attributes = (jArray[4] ?? new JObject()).ToObject<TransactionTraceAttributes>(serializer);

                return new TransactionTrace(timestamp, unknown1, unknown2, rootSegment, attributes);
            }


            private static JArray TryDecompress(object value)
            {
                if (value == null)
                    return null;

                var compressedTraceData = value.ToString();
                var traceData = Decompress(compressedTraceData);
                return JArray.Parse(traceData);
            }


            private static string Decompress(string compressedTraceData)
            {
                var bytes = Convert.FromBase64String(compressedTraceData);

                using (var memoryStream = new MemoryStream())
                using (var inflaterStream = new InflaterInputStream(memoryStream, new Inflater()))
                using (var streamReader = new StreamReader(inflaterStream))
                {
                    memoryStream.Write(bytes, 0, bytes.Length);
                    memoryStream.Flush();
                    memoryStream.Position = 0;
                    return streamReader.ReadToEnd();
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    public static class TransactionTraceExtensions
    {
        public static TransactionTraceSegment TryFindSegment(this TransactionTraceSegment segment, string name)
        {
            Contract.Assert(segment != null);
            Contract.Assert(name != null);

            if (segment.Name == name)
                return segment;

            if (segment.ChildSegments == null)
                return null;

            return segment
                .ChildSegments
                .Select(childSegment => childSegment.TryFindSegment(name))
                .Where(foundSegment => foundSegment != null)
                .FirstOrDefault();
        }

        public static TransactionTraceSegment TryFindSegment(this TransactionTraceSegment segment, string name, string parameterKey, string parameterValue)
        {
            Contract.Assert(segment != null);
            Contract.Assert(name != null);
            Contract.Assert(parameterKey != null);
            Contract.Assert(parameterValue != null);

            if (segment.Name == name
                && segment.Parameters != null
                && segment.Parameters.Contains(new KeyValuePair<string, object>(parameterKey, parameterValue)))
                return segment;

            if (segment.ChildSegments == null)
                return null;

            return segment
                .ChildSegments
                .Select(childSegment => childSegment.TryFindSegment(name, parameterKey, parameterValue))
                .Where(foundSegment => foundSegment != null)
                .FirstOrDefault();
        }

        public static bool ContainsSegment(this TransactionTrace trace, string name)
        {
            Contract.Assert(trace != null);
            Contract.Assert(name != null);

            var foundSegment = trace.RootSegment.TryFindSegment(name);
            return foundSegment != null;
        }

        public static TransactionTraceSegment TryFindSegment(this TransactionTrace trace, string name)
        {
            Contract.Assert(trace != null);
            Contract.Assert(name != null);

            return trace.RootSegment.TryFindSegment(name);
        }

        public static bool TryFindSegment(this TransactionTrace trace, string name, string parameterKey, string parameterValue)
        {
            Contract.Assert(trace != null);
            Contract.Assert(name != null);
            Contract.Assert(parameterKey != null);
            Contract.Assert(parameterValue != null);

            var foundSegment = trace.RootSegment.TryFindSegment(name, parameterKey, parameterValue);
            return foundSegment != null;
        }
    }

    [JsonConverter(typeof(TransactionTraceSegmentConverter))]
    public class TransactionTraceSegment
    {
        // index 0
        public readonly TimeSpan StartTimeOffset;

        // index 1
        public readonly TimeSpan EndTimeOffset;

        // index 2
        public readonly string Name;

        // index 3
        public readonly IDictionary<string, object> Parameters;

        // index 4
        public readonly IEnumerable<TransactionTraceSegment> ChildSegments;

        // index 5
        public readonly string ClassName;

        // index 6
        public readonly string MethodName;

        public TransactionTraceSegment(TimeSpan startTimeOffset, TimeSpan endTimeOffset, string name, IDictionary<string, object> parameters, IEnumerable<TransactionTraceSegment> childSegments, string className, string methodName)
        {
            StartTimeOffset = startTimeOffset;
            EndTimeOffset = endTimeOffset;
            Name = name;
            Parameters = parameters;
            ChildSegments = childSegments;
            ClassName = className;
            MethodName = methodName;
        }


        public class TransactionTraceSegmentConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return true;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var jArray = JArray.Load(reader);
                if (jArray == null)
                    throw new JsonSerializationException("Unable to create a jObject from reader.");

                var startTimeOffset = TimeSpan.FromMilliseconds((double)(jArray[0] ?? 0));
                var endTimeOffset = TimeSpan.FromMilliseconds((double)(jArray[1] ?? 0));
                var name = (jArray[2] ?? new JObject()).ToString();
                var parameters = (jArray[3] ?? new JObject()).ToObject<IDictionary<string, object>>(serializer);
                var childSegments = (jArray[4] ?? new JObject()).ToObject<IEnumerable<TransactionTraceSegment>>() ?? new List<TransactionTraceSegment>();
                var className = (jArray[5] ?? new JObject()).ToString();
                var methodName = (jArray[6] ?? new JObject()).ToString();

                return new TransactionTraceSegment(startTimeOffset, endTimeOffset, name, parameters, childSegments, className, methodName);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class TransactionTraceAttributes
    {
        [JsonProperty(PropertyName = "agentAttributes")]
        public readonly IDictionary<string, object> AgentAttributes;

        [JsonProperty(PropertyName = "intrinsics")]
        public readonly IDictionary<string, object> IntrinsicAttributes;

        [JsonProperty(PropertyName = "userAttributes")]
        public readonly IDictionary<string, object> UserAttributes;

        public TransactionTraceAttributes(IDictionary<string, object> agentAttributes, IDictionary<string, object> intrinsicAttributes, IDictionary<string, object> userAttributes)
        {
            AgentAttributes = agentAttributes;
            IntrinsicAttributes = intrinsicAttributes;
            UserAttributes = userAttributes;
        }


        public IDictionary<string, object> GetByType(TransactionTraceAttributeType attributeType)
        {
            IDictionary<string, object> attributes;
            switch (attributeType)
            {
                case TransactionTraceAttributeType.Intrinsic:
                    attributes = IntrinsicAttributes;
                    break;
                case TransactionTraceAttributeType.Agent:
                    attributes = AgentAttributes;
                    break;
                case TransactionTraceAttributeType.User:
                    attributes = UserAttributes;
                    break;
                default:
                    throw new NotImplementedException();
            }

            return attributes ?? new Dictionary<string, object>();
        }
    }

    public enum TransactionTraceAttributeType
    {
        Intrinsic,
        Agent,
        User,
    }
}
