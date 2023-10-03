// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DataTransport
{
    public enum DataTransportResponseStatus
    {
        RequestSuccessful,
        Retain,
        ReduceSizeIfPossibleOtherwiseDiscard,
        Discard
    }
}
