using System;
using System.Web;

namespace NewRelic.Agent.Extensions.Providers.TransactionContext
{
	/// <summary>
	/// ASP.NET transaction context backed by HttpContext.  Will correctly follow web requests across threads and deal with mid-thread interuption.
	/// </summary>
	public class AspTransactionContext<T> : IContextStorage<T>
	{
		private readonly String _key;

		/// <summary>
		/// Dude.
		/// </summary>
		public AspTransactionContext(String key)
		{
			_key = key;
		}

		Byte IContextStorage<T>.Priority { get { return 10; } }

		Boolean IContextStorage<T>.CanProvide { get { return HttpContext.Current != null; } }

		T IContextStorage<T>.GetData()
		{

			var httpContext = HttpContext.Current;
			if (httpContext == null)
				return default(T);

			return (T) httpContext.Items[_key];
		}

		void IContextStorage<T>.SetData(T value)
		{
			var httpContext = HttpContext.Current;
			if (httpContext == null)
				return;

			httpContext.Items[_key] = value;
		}

		void IContextStorage<T>.Clear()
		{
			HttpContext.Current?.Items?.Remove(_key);
		}
	}
}
