// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;

namespace NewRelic.Agent.Core.DataTransport.Client.Interfaces
{

    /// <summary>
    /// HttpContentHeaders wrapper to enable mocking in unit tests
    /// </summary>
    public interface IHttpContentWrapper
    {
        Stream ReadAsStream();
        IHttpContentHeadersWrapper Headers { get; }
    }
}
