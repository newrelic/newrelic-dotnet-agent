// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Api.Agent
{
    /// <summary>
    /// Provides access to span-specific methods in the New Relic API.
    /// </summary>
    public interface ISpan
    {
        /// <summary> Add a key/value pair to the transaction.  These are reported in errors and
        /// transaction traces.</summary>
        ///
        /// <param name="key">   The key name to add to the span attributes. Limited to 255-bytes.</param>
        /// <param name="value"> The value to add to the current span.  Values are limited to 255-bytes.</param>
        ISpan AddCustomAttribute(string key, object value);
    }
}
