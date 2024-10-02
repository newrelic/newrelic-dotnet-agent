// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.Collections
{
    public interface IResizableCappedCollection<T> : ICollection<T>
    {
        /// <summary>The maximum number of items the collection can contain.</summary>
        int Size { get; }
        /// <summary>Changes the number of items the collection can contain.</summary>
        /// <param name="newSize">The new maximum numer of items the collection can contain. CONTRACT: newSize < Int32.Max</param>
        void Resize(int newSize);

        // Represents the count of items that has been attempted to be added to the capped collection.
        int GetAddAttemptsCount();

        // Gets the current count of dropped items and resets the counter to 0
        int GetAndResetDroppedItemCount();
    }
}
