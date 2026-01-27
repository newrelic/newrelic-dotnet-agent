// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities;

public static class EventBus<T>
{
    private static event Action<T> Events = T => { };
    private static readonly ReaderWriterLock Lock = new ReaderWriterLock();
    private static readonly ReaderLockGuard ReaderLockGuard = new ReaderLockGuard(Lock);
    private static readonly WriterLockGuard WriterLockGuard = new WriterLockGuard(Lock);

    /// <summary>
    /// Subscribes to events on this event bus (events of type T).  If the same callback is subscribed multiple times you will receive multiple callbacks.
    /// </summary>
    /// <param name="callback">The method to call when an event on this bus is published.</param>
    public static void Subscribe(Action<T> callback)
    {
        using (WriterLockGuard.Acquire())
        {
            Events -= callback;
            Events += callback;
        }
    }

    /// <summary>
    /// Unsubscribes from events on this callback (events of type T). You must unsubscribe at least the same number of times as you subscribe to stop receiving callbacks.
    /// </summary>
    /// <param name="callback">The method that should no longer be called back when events on this bus are published.</param>
    public static void Unsubscribe(Action<T> callback)
    {
        using (WriterLockGuard.Acquire())
        {
            Events -= callback;
        }
    }

    /// <summary>
    /// Publish an event to this bus.  All subscribers to this bus (events of type T) will be called back once for each time they are subscribed.
    /// </summary>
    /// <param name="message">The event message that will be sent to all subscribers.</param>
    public static void Publish(T message)
    {
        // make a copy of the collection of event handlers and then call that
        // this has the potential of event handlers being called after unsubscribe but it allows us to wrap the event handler calls in a try/catch block so we don't have to worry about exceptions bubbling out of the event handlers
        // we don't wrap the whole thing in a lock because calling the event handlers while holding a lock is at very high risk of a deadlock
        Delegate events;
        using (ReaderLockGuard.Acquire())
        {
            events = Events;
        }

        foreach (Action<T> handler in events.GetInvocationList())
        {
            try
            {
                if (handler != null)
                    handler.Invoke(message);
            }
            catch (Exception exception)
            {
                Serilog.Log.Logger.Error(exception, "Exception thrown from event handler. Event handlers should not let exceptions bubble out of them.");
            }
        }
    }
}