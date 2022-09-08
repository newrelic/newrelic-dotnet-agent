// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Api
{
    /// <summary>
    /// This interface identifies functionality that is available to the API.
    /// Since the API refers to Spans, this object is named accordingly.
    /// </summary>
    public interface ISpan
    {
        ISpan AddCustomAttribute(string key, object value);

        ISpan SetName(string name);
    }
}
