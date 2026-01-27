// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DataTransport;

public struct DataTransportResponse<T>
{
    public readonly DataTransportResponseStatus Status;
    public readonly T ReturnValue;

    public DataTransportResponse(DataTransportResponseStatus status, T returnValue = default(T))
    {
        Status = status;
        ReturnValue = returnValue;
    }
}