// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    public interface IHttpContentHeadersWrapper
    {
        ICollection<string> ContentEncoding { get; }
    }
}
