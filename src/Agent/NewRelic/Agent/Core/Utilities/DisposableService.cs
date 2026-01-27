// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// An abstract base class for handling all of the boilerplate associated with a service that can be disposed.
/// </summary>
public abstract class DisposableService : IDisposable
{
    /// <summary>
    /// Subscriptions that will be disposed when service is disposed.
    /// </summary>
    protected Subscriptions _subscriptions { get; private set; }

    protected DisposableService()
    {
        _subscriptions = new Subscriptions();
    }

    /// <summary>
    /// Override if you need to dispose of anything.  Note: You don't have to dispose of _subscriptions here, it will be done for you.  Be sure to call base.Dispose()!
    /// </summary>
    public virtual void Dispose()
    {
        _subscriptions.Dispose();
    }
}
