// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Api.Agent
{
    /// <summary>
    /// Provides access to span-specific methods in the New Relic API.
    /// </summary>
    public interface ISpan
    {
        /// <summary>
        /// Add a key/value pair to the span.
        /// </summary>
        /// <param name="key"> The key name to add to the span attributes. Limited to 255-bytes.</param>
        /// <param name="value"> The value to add to the current span. Limited to 255-bytes.</param>
        /// <returns>ISpan to support builder pattern</returns>
        ISpan AddCustomAttribute(string key, object value);

        /// <summary>
        /// Sets the name of the current span.
        /// </summary>
        /// <param name="name">The new name for the span.</param>
        /// <returns>ISpan to support builder pattern</returns>
        ISpan SetName(string name);
    }
}
