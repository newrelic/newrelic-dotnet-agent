using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    [JsonConverter(typeof(TransactionTraceConverter))]
    public class TransactionTrace
    {
        // index 0
        public readonly DateTime Timestamp;

        // index 1
        public readonly Object Unknown1;

        // index 2
        public readonly Object Unknown2;

        //index 3
        public readonly TransactionTraceSegment RootSegment;

        // index 4
        public readonly TransactionTraceAttributes Attributes;

        public TransactionTrace(DateTime timestamp, Object unknown1, Object unknown2, TransactionTraceSegment rootSegment, TransactionTraceAttributes attributes)
        {
            Timestamp = timestamp;
            Unknown1 = unknown1;
            RootSegment = rootSegment;
            Unknown2 = unknown2;
            Attributes = attributes;
        }

        public class TransactionTraceConverter : JsonConverter
        {
            public override Boolean CanConvert(Type objectType)
            {
                return true;
            }

            public override Object ReadJson(JsonReader reader, Type objectType, Object existingValue, JsonSerializer serializer)
            {
                var jArray = JArray.Load(reader);
                if (jArray == null)
                    throw new JsonSerializationException("Unable to create a jObject from reader.");

                var timestamp = new DateTime(1970, 01, 01) + TimeSpan.FromSeconds((Double)(jArray[0] ?? 0));
                var unknown1 = jArray[1];
                var unknown2 = jArray[2];
                var rootSegment = (jArray[3] ?? new JObject()).ToObject<TransactionTraceSegment>(serializer);
                var attributes = (jArray[4] ?? new JObject()).ToObject<TransactionTraceAttributes>(serializer);

                return new TransactionTrace(timestamp, unknown1, unknown2, rootSegment, attributes);
            }

            private static JArray TryDecompress(Object value)
            {
                if (value == null)
                    return null;

                var compressedTraceData = value.ToString();
                var traceData = Decompress(compressedTraceData);
                return JArray.Parse(traceData);
            }

            private static String Decompress(String compressedTraceData)
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

            public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    public static class TransactionTraceExtensions
    {
        public static TransactionTraceSegment TryFindSegment(this TransactionTraceSegment segment, String name)
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

        public static TransactionTraceSegment TryFindSegment(this TransactionTraceSegment segment, String name, String parameterKey, String parameterValue)
        {
            Contract.Assert(segment != null);
            Contract.Assert(name != null);
            Contract.Assert(parameterKey != null);
            Contract.Assert(parameterValue != null);

            if (segment.Name == name
                && segment.Parameters != null
                && segment.Parameters.Contains(new KeyValuePair<String, Object>(parameterKey, parameterValue)))
                return segment;

            if (segment.ChildSegments == null)
                return null;

            return segment
                .ChildSegments
                .Select(childSegment => childSegment.TryFindSegment(name, parameterKey, parameterValue))
                .Where(foundSegment => foundSegment != null)
                .FirstOrDefault();
        }

        public static Boolean ContainsSegment(this TransactionTrace trace, String name)
        {
            Contract.Assert(trace != null);
            Contract.Assert(name != null);

            var foundSegment = trace.RootSegment.TryFindSegment(name);
            return foundSegment != null;
        }

        public static TransactionTraceSegment TryFindSegment(this TransactionTrace trace, String name)
        {
            Contract.Assert(trace != null);
            Contract.Assert(name != null);

            return trace.RootSegment.TryFindSegment(name);
        }

        public static Boolean TryFindSegment(this TransactionTrace trace, String name, String parameterKey, String parameterValue)
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
        public readonly String Name;

        // index 3
        public readonly IDictionary<String, Object> Parameters;

        // index 4
        public readonly IEnumerable<TransactionTraceSegment> ChildSegments;

        // index 5
        public readonly String ClassName;

        // index 6
        public readonly String MethodName;

        public TransactionTraceSegment(TimeSpan startTimeOffset, TimeSpan endTimeOffset, String name, IDictionary<String, Object> parameters, IEnumerable<TransactionTraceSegment> childSegments, String className, String methodName)
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
            public override Boolean CanConvert(Type objectType)
            {
                return true;
            }

            public override Object ReadJson(JsonReader reader, Type objectType, Object existingValue, JsonSerializer serializer)
            {
                var jArray = JArray.Load(reader);
                if (jArray == null)
                    throw new JsonSerializationException("Unable to create a jObject from reader.");

                var startTimeOffset = TimeSpan.FromMilliseconds((Double)(jArray[0] ?? 0));
                var endTimeOffset = TimeSpan.FromMilliseconds((Double)(jArray[1] ?? 0));
                var name = (jArray[2] ?? new JObject()).ToString();
                var parameters = (jArray[3] ?? new JObject()).ToObject<IDictionary<String, Object>>(serializer);
                var childSegments = (jArray[4] ?? new JObject()).ToObject<IEnumerable<TransactionTraceSegment>>() ?? new List<TransactionTraceSegment>();
                var className = (jArray[5] ?? new JObject()).ToString();
                var methodName = (jArray[6] ?? new JObject()).ToString();

                return new TransactionTraceSegment(startTimeOffset, endTimeOffset, name, parameters, childSegments, className, methodName);
            }

            public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class TransactionTraceAttributes
    {
        [JsonProperty(PropertyName = "agentAttributes")]
        public readonly IDictionary<String, Object> AgentAttributes;

        [JsonProperty(PropertyName = "intrinsics")]
        public readonly IDictionary<String, Object> IntrinsicAttributes;

        [JsonProperty(PropertyName = "userAttributes")]
        public readonly IDictionary<String, Object> UserAttributes;

        public TransactionTraceAttributes(IDictionary<String, Object> agentAttributes, IDictionary<String, Object> intrinsicAttributes, IDictionary<String, Object> userAttributes)
        {
            AgentAttributes = agentAttributes;
            IntrinsicAttributes = intrinsicAttributes;
            UserAttributes = userAttributes;
        }

        public IDictionary<String, Object> GetByType(TransactionTraceAttributeType attributeType)
        {
            IDictionary<String, Object> attributes;
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

            return attributes ?? new Dictionary<String, Object>();
        }
    }

    public enum TransactionTraceAttributeType
    {
        Intrinsic,
        Agent,
        User,
    }
}
