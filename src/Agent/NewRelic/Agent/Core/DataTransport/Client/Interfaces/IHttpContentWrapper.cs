// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport.Client.Interfaces
{

    /// <summary>
    /// HttpContentHeaders wrapper to enable mocking in unit tests
    /// </summary>
    public interface IHttpContentWrapper
    {
        Task<Stream> ReadAsStreamAsync();
        IHttpContentHeadersWrapper Headers { get; }
    }
}
