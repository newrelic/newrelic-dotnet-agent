// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// Custom exception thrown when the message payload exceeds the maximum configured size
    /// </summary>
    public class PayloadSizeExceededException : Exception
    {
    }
}
