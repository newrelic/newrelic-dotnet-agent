using System;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Dispatchers.Utilities;
using NewRelic.WeakActions;

namespace NewRelic.Dispatchers
{
	// ReSharper disable StaticFieldInGenericType
	public static class EventBus<T>
	{
		//[NotNull] private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(EventBus<T>));
		private static event Action<T> Events = T => { };
		[NotNull] private static readonly ReaderWriterLock Lock = new ReaderWriterLock();
		[NotNull] private static readonly ReaderLockGuard ReaderLockGuard = new ReaderLockGuard(Lock);
		[NotNull] private static readonly WriterLockGuard WriterLockGuard = new WriterLockGuard(Lock);

		/// <summary>
		/// Subscribes to events on this event bus (events of type T).  If the same callback is subscribed multiple times you will receive multiple callbacks.
		/// </summary>
		/// <param name="callback">The method to call when an event on this bus is published.</param>
		public static void Subscribe([NotNull] Action<T> callback)
		{
			using (WriterLockGuard.Acquire())
			{
				Events -= callback;
				Events += callback;
			}
		}

		/// <summary>
		/// Subscribes to events on this event bus (events of type T) but does not retain a reference for the purpose of garbage collection.
		/// </summary>
		/// <remarks>The weakCallback will be automatically unsubscribed when the subscriber is garbage collected.  You can also call Unsubscribe(IWeakAction) to unsubscribe sooner.</remarks>
		/// <param name="weakCallback">The method to call when an event on this bus is published.</param>
		public static void Subscribe([NotNull] IWeakAction<T> weakCallback)
		{
			using (WriterLockGuard.Acquire())
			{
				Events -= weakCallback.Action;
				Events += weakCallback.Action;
			}
		}

		/// <summary>
		/// Subscribes to events on this event bus (events of type T) but does not retain a reference for the purpose of garbage collection.
		/// </summary>
		/// <remarks>You cannot Unsubscribe a subscription that is setup in this way, it will automatically be unsubscribed when the subscriber is garbage collected.  If you want to be able to unsubscribe a weak subscription, use Subscribe(IWeakAction) overload.</remarks>
		/// <param name="callback">The method to call when an event on this bus is published.</param>
		public static void WeakSubscribe([NotNull] Action<T> callback)
		{
			var weakCallback = WeakActionUtilities.MakeWeak<T>(callback, action => Events -= action);

			using (WriterLockGuard.Acquire())
			{
				Events += weakCallback.Action;
			}
		}

		/// <summary>
		/// Unsubscribes from events on this callback (events of type T). You must unsubscribe at least the same number of times as you subscribe to stop receiving callbacks.
		/// </summary>
		/// <param name="callback">The method that should no longer be called back when events on this bus are published.</param>
		public static void Unsubscribe([NotNull] Action<T> callback)
		{
			using (WriterLockGuard.Acquire())
			{
				Events -= callback;
			}
		}

		/// <summary>
		/// Unsubscribes from events on this callback (events of type T). You must unsubscribe at least the same number of times as you subscribe to stop receiving callbacks.
		/// </summary>
		/// <remarks>Use this overload when you want to have a subscriber whose subscription lasts either until it is unsubscribed or until the object that the weakCallback points to is garbage collected.</remarks>
		/// <param name="weakCallback">The method that should no longer be called back when events on this bus are published.</param>
		public static void Unsubscribe([NotNull] IWeakAction<T> weakCallback)
		{
			using (WriterLockGuard.Acquire())
			{
				Events -= weakCallback.Action;
			}
		}

		/// <summary>
		/// Publish an event to this bus.  All subscribers to this bus (events of type T) will be called back once for each time they are subscribed.
		/// </summary>
		/// <param name="message">The event message that will be sent to all subscribers.</param>
		public static void Publish([NotNull] T message)
		{
			// make a copy of the collection of event handlers and then call that
			// this has the potential of event handlers being called after unsubscribe but it allows us to wrap the event handler calls in a try/catch block so we don't have to worry about exceptions bubbling out of the event handlers
			// we don't wrap the whole thing in a lock because calling the event handlers while holding a lock is at very high risk of a deadlock
			Delegate events;
			using (ReaderLockGuard.Acquire())
			{
				events = Events;
			}

			// ReSharper disable once PossibleNullReferenceException
			foreach (Action<T> handler in events.GetInvocationList())
			{
				try
				{
					if (handler != null)
						handler.Invoke(message);
				}
				catch
				{
					//Log.Error("Exception thrown from event handler.  Event handlers should not let exceptions bubble out of them.", exception);
				}
			}
		}

		/// <summary>
		/// Publish an event to this bus on a background thread.  All subscribers to this bus (events of type T) will be called back once for each time they are subscribed.
		/// </summary>
		/// <param name="message">The event message that will be sent to all subscribers.</param>
		public static void PublishAsync([NotNull] T message)
		{
			ThreadPool.QueueUserWorkItem(_ => Publish(message));
		}
	}
}
