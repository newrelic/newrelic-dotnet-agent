using System;
using JetBrains.Annotations;

namespace NewRelic.Dispatchers
{
	public class EventSubscription<T> : IDisposable
	{
		[NotNull] private readonly Action<T> _callback;
 
		public EventSubscription([NotNull] Action<T> callback)
		{
			_callback = callback;
			EventBus<T>.Subscribe(_callback);
		}

		public void Dispose()
		{
			EventBus<T>.Unsubscribe(_callback);
		}
	}
}
