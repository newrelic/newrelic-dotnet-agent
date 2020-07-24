using System;
using JetBrains.Annotations;
using NewRelic.WeakActions;

namespace NewRelic.Dispatchers
{
    public class WeakEventSubscription<T> : IDisposable
    {
        [NotNull] private readonly IWeakAction<T> _callback;

        public WeakEventSubscription([NotNull] Action<T> callback)
        {
            _callback = WeakActionUtilities.MakeWeak(callback, garbageCollectedAction => EventBus<T>.Unsubscribe(garbageCollectedAction));
            EventBus<T>.Subscribe(_callback);
        }

        public void Dispose()
        {
            EventBus<T>.Unsubscribe(_callback);
        }
    }
}
