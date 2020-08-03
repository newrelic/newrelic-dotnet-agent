// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.WeakActions
{
    public interface IWeakAction<T>
    {
        Action<T> Action { get; }
    }

    public interface IWeakAction<T1, T2>
    {
        Action<T1, T2> Action { get; }
    }
}
