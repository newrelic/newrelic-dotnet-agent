// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using Grpc.Core;

namespace NewRelic.Agent.Core.DataTransport;

public interface IGrpcUnaryWrapper<TRequest, TResponse>
{
    bool IsConnected { get; }
    bool CreateChannel(string host, int port, bool ssl, int connectTimeoutMs, CancellationToken cancellationToken);
    bool TrySendData(TRequest item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken, out TResponse response);
    void Shutdown();
}
