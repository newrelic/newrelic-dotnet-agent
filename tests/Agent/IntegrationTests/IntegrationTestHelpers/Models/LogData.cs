// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
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
        [JsonProperty("entity.guid")]
        public string EntityGuid { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }
    }

    public class LogLine
    {
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("level")]
        public string Level { get; set; }

        [JsonProperty("attributes")]
        public LogAttributes Attributes { get; set; }
    }

    public class LogAttributes
    {
        [JsonProperty("spanid", NullValueHandling = NullValueHandling.Ignore)]
        public string Spanid { get; set; }

        [JsonProperty("traceid", NullValueHandling = NullValueHandling.Ignore)]
        public string Traceid { get; set; }
    }
}
