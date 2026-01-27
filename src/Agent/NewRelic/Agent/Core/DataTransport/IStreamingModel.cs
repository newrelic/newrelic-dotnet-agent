// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DataTransport;

public interface IStreamingModel
{
    string DisplayName { get; }
}

public interface IStreamingBatchModel<TRequest> where TRequest:IStreamingModel
{
    int Count { get; }
}