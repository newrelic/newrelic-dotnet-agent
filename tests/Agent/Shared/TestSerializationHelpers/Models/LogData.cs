// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class LogEventData
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };

        public static LogEventData[] FromJson(string json) => JsonConvert.DeserializeObject<LogEventData[]>(json, Settings);

        [JsonProperty("common")]
        public LogEventDataCommon Common { get; set; }

        [JsonProperty("logs")]
        public LogLine[] Logs { get; set; }
    }

    public partial class LogEventDataCommon
    {
        [JsonProperty("attributes")]
        public LogEventDataCommonAttributes Attributes { get; set; }
    }

    public class LogEventDataCommonAttributes
    {
        [JsonProperty("entity.name")]
        public string EntityName { get; set; }

        [JsonProperty("entity.guid")]
        public string EntityGuid { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }
    }

    public class LogLine
    {
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty("level")]
        public string Level { get; set; }

        [JsonProperty("span.id", NullValueHandling = NullValueHandling.Ignore)]
        public string Spanid { get; set; }

        [JsonProperty("trace.id", NullValueHandling = NullValueHandling.Ignore)]
        public string Traceid { get; set; }

        [JsonProperty("error.stack", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorStack { get; set; }

        [JsonProperty("error.message", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorMessage { get; set; }

        [JsonProperty("error.class", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorClass { get; set; }

        [JsonProperty("attributes", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Attributes { get; set; }
    }
}
