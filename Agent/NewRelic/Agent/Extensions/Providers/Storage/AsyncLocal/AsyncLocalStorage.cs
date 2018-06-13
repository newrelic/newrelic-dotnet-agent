using NewRelic.Agent.Extensions.Providers;
using System.Threading;

namespace NewRelic.Providers.Storage.AsyncLocal
{
	/// <summary>
	/// Multiple instances of this class will share state per type T because we use a
	/// static AsyncLocal instance.  AsyncLocal instantiation can be very expensive.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class AsyncLocalStorage<T> : IContextStorage<T>
	{
		private static readonly AsyncLocal<T> _context = new AsyncLocal<T>();

		public byte Priority => 2;
		public bool CanProvide => true;

		public T GetData()
		{
			return _context.Value;
		}

		public void SetData(T value)
		{
			_context.Value = value;
		}

		public void Clear()
		{
			_context.Value = default(T);
		}
	}
}