// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Api.Agent;

/// <summary>
/// Transport types used to receive Distributed Trace payloads.
/// </summary>
public enum TransportType
{
    Unknown = 0,
    HTTP = 1,
    HTTPS = 2,
    Kafka = 3,
    JMS = 4,
    IronMQ = 5,
    AMQP = 6,
    Queue = 7,
    Other = 8
}

/// <summary>
/// Constants for use with NewRelic API methods.
/// </summary>
public static class Constants
{
    /// <summary>
    /// This is the key-part that the agent recognizes when trying to find a DistributedTracePayload, typically passed as a KeyValuePair in the header of a request.
    /// </summary>
    public const string DistributedTracePayloadKey = "Newrelic";
}