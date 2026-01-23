// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DistributedTracing;

public enum IngestErrorType
{
    Version,
    NullPayload,
    ParseException,
    OtherException,
    NotTraceable,
    NotTrusted,

    TraceParentParseException,
    TraceStateParseException,
    TraceStateInvalidNrEntry,
    TraceStateNoNrEntry,

    TraceContextAcceptException,
    TraceContextCreateException,

    None,

}

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

        if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(appId))
        {
            Log.Finest("Did not generate payload because AccountId or PrimaryApplicationId were not populated. This is normal for requests occurring before round trip with server has completed.");
            return null;
        }

        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(traceId))
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

    public static DistributedTracePayload TryBuildIncomingPayloadFromJson(string json, List<IngestErrorType> errors)
    {
        if (string.IsNullOrEmpty(json))
        {
            //throw new DistributedTraceAcceptPayloadNullException("json input cannot be null or empty string");
            errors.Add(IngestErrorType.NullPayload);
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<DistributedTracePayload>(json);
        }
        catch (Exception ex) when (ex is DistributedTraceAcceptPayloadParseException || ex is JsonException)
        {
            errors.Add(IngestErrorType.ParseException);
            return null;
        }
        catch (DistributedTraceAcceptPayloadVersionException)
        {
            errors.Add(IngestErrorType.Version);
            return null;
        }

    }

    /// <summary>
    /// Serialize a DistributedTracePayload <paramref name="payload"/> to an, optionally pretty, JSON string.
    /// </summary>
    /// <param name="payload">The DistributedTracePayload</param>
    /// <param name="pretty">When true, the JSON string will have extra whitespace/new lines. When false, the JSON will be compact.</param>
    /// <returns>The serialized JSON string</returns>
    public string ToJson(bool pretty = false)
    {
        return JsonConvert.SerializeObject(this, pretty ? Formatting.Indented : Formatting.None);
    }

    /// <summary>
    /// Serializes this DistributedTracePayload to JSON and Base64 encodes it."/>
    /// </summary>
    /// <returns>The serialized and encoded data.</returns>
    /// used by Lambda
    public string SerializeAndEncodeDistributedTracePayload()
    {
        var serializedData = ToJson();
        if (serializedData == null)
        {
            throw new NullReferenceException("serializedData");
        }

        return Strings.Base64Encode(serializedData);
    }

    /// <summary>
    /// Serializes <paramref name="data"/> to JSON and Base64 encodes it./>
    /// </summary>
    /// <param name="data">The data to encode. Must not be null.</param>
    /// <returns>The serialized and encoded data.</returns>
    public static string SerializeAndEncodeDistributedTracePayload(DistributedTracePayload data)
    {
        var serializedData = data.ToJson();
        if (serializedData == null)
        {
            throw new NullReferenceException("serializedData");
        }

        return Strings.Base64Encode(serializedData);
    }

    public static DistributedTracePayload TryDecodeAndDeserializeDistributedTracePayload(string encodedString)
    {
        var stringToConvert = encodedString?.Trim();
        if (!string.IsNullOrEmpty(stringToConvert))
        {
            var firstChar = stringToConvert[0];
            if (firstChar != '{' && firstChar != '[')
            {
                stringToConvert = Strings.TryBase64Decode(stringToConvert);
                if (stringToConvert == null)
                {
                    Log.Debug("Could not decode distributed trace payload string: " + encodedString);
                    return null;
                }
            }
        }

        return TryBuildIncomingPayloadFromJson(stringToConvert);
    }

    public static DistributedTracePayload TryDecodeAndDeserializeDistributedTracePayload(string encodedString, string agentTrustKey, List<IngestErrorType> errors)
    {
        var stringToConvert = encodedString?.Trim();
        if (!string.IsNullOrEmpty(stringToConvert))
        {
            var firstChar = stringToConvert[0];
            if (firstChar != '{' && firstChar != '[')
            {
                stringToConvert = Strings.TryBase64Decode(stringToConvert);
                if (stringToConvert == null)
                {
                    Log.Debug("Could not decode distributed trace payload string: " + encodedString);
                    errors.Add(IngestErrorType.ParseException);
                    return null;
                }
            }
        }
        else
        {
            errors.Add(IngestErrorType.NullPayload);
            return null;
        }

        var payload = TryBuildIncomingPayloadFromJson(stringToConvert, errors);

        if (payload != null)
        {
            if (payload.Guid == null && payload.TransactionId == null)
            {
                Log.Debug("Incoming Guid and TransactionId were null, which is invalid for a Distributed Trace payload.");
                errors.Add(IngestErrorType.NotTraceable);
                return null;
            }

            var incomingTrustKey = payload.TrustKey ?? payload.AccountId;
            var isTrusted = incomingTrustKey == agentTrustKey;

            if (!isTrusted)
            {
                Log.Debug($"Incoming trustKey or accountId [{incomingTrustKey}] not trusted, distributed trace payload will be ignored.");
                errors.Add(IngestErrorType.NotTrusted);
                return null;
            }
        }

        return payload;
    }
}
