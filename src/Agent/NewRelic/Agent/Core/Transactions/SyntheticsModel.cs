// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Core;
using NewRelic.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Transactions
{
    /// <summary>
    /// ASYNC PROJECT NOTE: Consider moving some of these methods in this model to NewRelic.Agent.Core.Wrapper.Agent.Synthetics.SyntheticsHeaderHandler
    /// once the legacy agent is deprecated - this will ensure that we have a lightweight model that has a single responsibility while allowing the SyntheticsHeaderHandler 
    /// to do all of the data processing. 
    /// </summary>
    [JsonConverter(typeof(JsonArrayConverter))]
    public class SyntheticsHeader
    {
        public const int MaxEventCount = 200;
        public const int MaxTraceCount = 20;
        public const string HeaderKey = "X-NewRelic-Synthetics";

        public string EncodingKey;
        public const long SupportedHeaderVersion = 1;

        [JsonArrayIndex(Index = 0)]
        public readonly long Version;

        [JsonArrayIndex(Index = 1)]
        public readonly long AccountId;

        [JsonArrayIndex(Index = 2)]
        public readonly string ResourceId;

        [JsonArrayIndex(Index = 3)]
        public readonly string JobId;

        [JsonArrayIndex(Index = 4)]
        public readonly string MonitorId;

        public SyntheticsHeader(long version, long accountId, string resourceId, string jobId, string monitorId)
        {
            Version = version;
            AccountId = accountId;
            ResourceId = resourceId;
            JobId = jobId;
            MonitorId = monitorId;
        }

        public bool IsValidSyntheticsDataForSave()
        {
            return (!string.IsNullOrEmpty(ResourceId) && !string.IsNullOrEmpty(JobId) && !string.IsNullOrEmpty(MonitorId));
        }

        public static SyntheticsHeader TryCreate(IEnumerable<long> trustedAccountIds, string obfuscatedHeader, string encodingKey)
        {
            try
            {
                if (trustedAccountIds == null)
                    throw new ArgumentNullException("trustedAccountIds");
                if (obfuscatedHeader == null)
                    throw new ArgumentNullException("obfuscatedHeader");
                if (encodingKey == null)
                    throw new ArgumentNullException("encodingKey");

                var serializedHeader = Deobfuscate(obfuscatedHeader, encodingKey);

                // Manually deserialize the version number first because it is easy to do and if it fails it provides more specific info than a general deserialization failure
                var version = DeserializeVersion(serializedHeader);
                if (IsUnsupportedVersion(version))
                    return null;

                var syntheticsHeader = JsonConvert.DeserializeObject<SyntheticsHeader>(serializedHeader);
                if (syntheticsHeader == null)
                    throw new JsonSerializationException("Failed to deserialize " + HeaderKey + " header. Expected object but got null");

                syntheticsHeader.EncodingKey = encodingKey;

                if (IsUntrustedAccount(syntheticsHeader, trustedAccountIds))
                    return null;

                return syntheticsHeader;
            }
            catch (Exception exception)
            {
                Log.Warn(exception, "TryCreate() failed");
                return null;
            }
        }

        public string TryGetObfuscated()
        {
            try
            {
                var serializedHeader = JsonConvert.SerializeObject(this);
                if (serializedHeader == null)
                    throw new JsonSerializationException("Failed to serialize synthetics header.  Expected string out, received null.");

                return Obfuscate(serializedHeader, EncodingKey);
            }
            catch (Exception exception)
            {
                Log.Warn(exception, "TryGetObfuscated() failed");
                return null;
            }
        }

        private static string Obfuscate(string serializedHeader, string encodingKey)
        {
            return Strings.Base64Encode(serializedHeader, encodingKey);
        }

        private static string Deobfuscate(string obfuscatedHeader, string encodingKey)
        {
            return Strings.Base64Decode(obfuscatedHeader, encodingKey);
        }

        private static long DeserializeVersion(string jsonSerializedHeader)
        {
            if (jsonSerializedHeader == null)
                throw new ArgumentNullException("jsonSerializedHeader");

            var parsed = JArray.Parse(jsonSerializedHeader);
            if (parsed == null)
                throw new JsonSerializationException("Failed to parse X-NewRelic-Synthetics header as an array: " + jsonSerializedHeader);

            var versionToken = parsed[0];
            if (versionToken == null)
                throw new JsonSerializationException("Failed to get version from first item in X-NewRelic-Synthetics header array: " + jsonSerializedHeader);
            if (versionToken.Type != JTokenType.Integer)
                throw new JsonSerializationException("Failed to parse version as an integer in X-NewRelic-Synthetics header array: " + jsonSerializedHeader);
            return versionToken.ToObject<long>();
        }

        /// <summary>
        /// IsUnsupportedVersion: https://source.datanerd.us/agents/agent-specs/blob/master/Synthetics-PORTED.md#verify-version
        // TDDO: evaluate to see if this will need to change for backwards compatibility: i.e.version =< SupportedHeaderVersion
        // if version = 1 & SupportedHeaderVersion = 2 then true
        // if version = 2 & SupportedHeaderVersion = 2 then true
        // if version = 3 & SupportedHeaderVersion = 2 then false
        /// </summary>
        private static bool IsUnsupportedVersion(long version)
        {
            return (version != SupportedHeaderVersion);
        }

        /// <summary>
        /// IsUntrustedAccount: https://source.datanerd.us/agents/agent-specs/blob/master/Synthetics-PORTED.md#verify-account-id
        /// </summary>
        private static bool IsUntrustedAccount(SyntheticsHeader syntheticsHeader, IEnumerable<long> trustedAccountIds)
        {
            return !(trustedAccountIds.Contains(syntheticsHeader.AccountId));
        }
    }
}
