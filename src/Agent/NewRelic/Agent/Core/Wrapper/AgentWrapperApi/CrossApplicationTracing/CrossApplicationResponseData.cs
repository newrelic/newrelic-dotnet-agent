// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;

// Note: this data is referred to as "AppData" in the CAT spec.
[JsonConverter(typeof(CrossApplicationResponseDataJsonConverter))]
public class CrossApplicationResponseData
{
    public readonly string CrossProcessId;
    public readonly string TransactionName;
    public readonly float QueueTimeInSeconds;
    public readonly float ResponseTimeInSeconds;
    public readonly long ContentLength;
    public readonly string TransactionGuid; //optional
    public readonly bool Unused; //optional

    // For backwards compatibility we need to support deserializing AppData that is missing fields 5 and 6
    public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, long contentLength)
    {
        CrossProcessId = crossProcessId;
        TransactionName = transactionName;
        QueueTimeInSeconds = queueTimeInSeconds;
        ResponseTimeInSeconds = responseTimeInSeconds;
        ContentLength = contentLength;
    }

    // For backwards compatibility we need to support deserializing AppData that is missing field 6
    public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, long contentLength, string transactionGuid) : this(crossProcessId, transactionName, queueTimeInSeconds, responseTimeInSeconds, contentLength)
    {
        TransactionGuid = transactionGuid;
    }

    public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, long contentLength, string transactionGuid, bool unused) : this(crossProcessId, transactionName, queueTimeInSeconds, responseTimeInSeconds, contentLength, transactionGuid)
    {
        Unused = unused;
    }
}