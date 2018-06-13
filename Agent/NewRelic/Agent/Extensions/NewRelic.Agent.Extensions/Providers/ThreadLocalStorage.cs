using System.Threading;

namespace NewRelic.Agent.Extensions.Providers
{
	/// <summary>
	/// A general use storage context backed by a ThreadLocal variable.  Will work well whenever transactions are single threaded and uninterrupted.
	/// </summary>
	public class ThreadLocalStorage<T> : IContextStorage<T>
	{
		private readonly ThreadLocal<T> _threadLocal;

		public ThreadLocalStorage(string key)
		{
			_threadLocal = new ThreadLocal<T>();
		}

		byte IContextStorage<T>.Priority => 1;
		bool IContextStorage<T>.CanProvide => true;

		T IContextStorage<T>.GetData()
		{
			return _threadLocal.Value;
		}

		void IContextStorage<T>.SetData(T value)
		{
			_threadLocal.Value = value;
		}

		void IContextStorage<T>.Clear()
		{
			_threadLocal.Value = default(T);
		}
	}
}
