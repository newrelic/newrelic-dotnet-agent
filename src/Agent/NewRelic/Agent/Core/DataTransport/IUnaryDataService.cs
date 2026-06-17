// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Collections;

namespace NewRelic.Agent.Core.DataTransport;

public interface IUnaryDataService<TRequest, TRequestBatch, TResponse> : IDisposable
    where TRequest : IStreamingModel
{
    bool IsServiceAvailable { get; }
    bool IsServiceEnabled { get; }
    bool IsSending { get; }
    string EndpointHost { get; }
    int EndpointPort { get; }
    bool EndpointSsl { get; }
    int BatchSizeConfigValue { get; }
    float? EndpointTestFlaky { get; }
    int? EndpointTestDelayMs { get; }
    int TimeoutConnectMs { get; }
    int TimeoutSendDataMs { get; }
    void Shutdown(bool withRestart);
    void StartConsumingCollection(PartitionedBlockingCollection<TRequest> collection);
    void Wait(int millisecondsTimeout = -1);
    bool ReadAndValidateConfiguration();
}
