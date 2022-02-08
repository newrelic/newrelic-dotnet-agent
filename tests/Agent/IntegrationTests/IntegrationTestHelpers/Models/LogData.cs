// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NewRelic.Agent.IntegrationTestHelpers.Models.LogEventData
{
    public partial class LogEventData
    {
        [JsonProperty("common")]
        public Common Common { get; set; }

        [JsonProperty("logs")]
        public Log[] Logs { get; set; }
    }

    public partial class Common
    {
        [JsonProperty("attributes")]
        public CommonAttributes Attributes { get; set; }
    }

    public partial class CommonAttributes
    {
        [JsonProperty("entity.name")]
        public string EntityName { get; set; }

        [JsonProperty("entity.type")]
        public string EntityType { get; set; }

        [JsonProperty("entity.guid")]
        public string EntityGuid { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("plugin.type")]
        public string PluginType { get; set; }
    }

    public partial class Log
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

    public partial class LogAttributes
    {
        [JsonProperty("spanid", NullValueHandling = NullValueHandling.Ignore)]
        public string Spanid { get; set; }

        [JsonProperty("traceid", NullValueHandling = NullValueHandling.Ignore)]
        public string Traceid { get; set; }
    }

    public partial class LogEventData
    {
        public static LogEventData[] FromJson(string json) => JsonConvert.DeserializeObject<LogEventData[]>(json, Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this LogEventData[] self) => JsonConvert.SerializeObject(self, Converter.Settings);
    }

    internal static class Converter
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
    }
}
