using System;
using JetBrains.Annotations;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities
{
    public static class FuncExtension
    {
		/// <summary>
		/// Returns a memoized copy of this func.  The original func
		/// will be invoked on the first invocation and its return 
		/// value will be cached.  All subsequent invocations will return the
		/// cached value.
		/// 
		/// The new func is thread safe - it can be invoked different threads
		/// but it is guaranteed to only invoke the original func once.
		/// </summary>
		public static Func<R> Memoize<R>([NotNull] this Func<R> func)
		{
			return new FuncCache<R>(func).Invoke;
		}

		private class FuncCache<R>
		{
			private R _cachedValue = default(R);
			private readonly Func<R> _func;

			public FuncCache(Func<R> func)
			{
				_func = func;
			}

			public R Invoke()
			{
				lock (this)
				{
					if (_cachedValue == null)
					{
						_cachedValue = _func.Invoke();

					}
					return _cachedValue;
				}
			}
		}

	}
}
