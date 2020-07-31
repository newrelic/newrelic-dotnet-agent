// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    /// <summary>
    /// These fields are all accessed prior to the end of the transaction (for RUM)
    /// and after the transaction ends. Some of the fields are also accessed for CAT too.
    /// This means all the underlying fields need to be accessable on multiple threads.
    /// </summary>
    public interface ITransactionAttributeMetadata
    {
        IEnumerable<KeyValuePair<string, string>> RequestParameters { get; }
        IEnumerable<KeyValuePair<string, string>> ServiceParameters { get; }
        IEnumerable<KeyValuePair<string, object>> UserAttributes { get; }
        IEnumerable<KeyValuePair<string, object>> UserErrorAttributes { get; }

        string Uri { get; }
        string OriginalUri { get; }
        string ReferrerUri { get; }

        int? HttpResponseStatusCode { get; }
        TimeSpan? QueueTime { get; }
    }
}
