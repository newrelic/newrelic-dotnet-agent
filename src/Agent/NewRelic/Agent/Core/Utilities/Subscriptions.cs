using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Utilities
{
    /// <summary>
    /// Thread safe disposable event subscription manager.
    /// </summary>
    public class Subscriptions : IDisposable
    {
        [NotNull] private readonly ICollection<IDisposable> _subscriptions = new List<IDisposable>();

        public void Add<T>([NotNull] Action<T> callback)
        {
            lock (_subscriptions)
            {
                _subscriptions.Add(new EventSubscription<T>(callback));
            }
        }

        public void Add<TRequest, TResponse>([NotNull] RequestBus<TRequest, TResponse>.RequestHandler requestHandler)
        {
            lock (_subscriptions)
            {
                _subscriptions.Add(new RequestSubscription<TRequest, TResponse>(requestHandler));
            }
        }

        public void Dispose()
        {
            lock (_subscriptions)
            {
                foreach (var subscription in _subscriptions)
                    if (subscription != null)
                        subscription.Dispose();

                _subscriptions.Clear();
            }
        }
    }
}
