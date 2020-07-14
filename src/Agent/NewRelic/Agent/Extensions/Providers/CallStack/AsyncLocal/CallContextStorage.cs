using JetBrains.Annotations;

namespace NewRelic.Providers.CallStack.AsyncLocal
{
	public class CallContextStorage<T> : CallContextStorageBase<T>
	{
		[NotNull]
		private readonly AsyncLocal<T> _storage;

		public CallContextStorage(string key)
		{
			_storage = new AsyncLocal<T>(key);
		}
		
		public override T GetData()
		{
			return _storage.Value;
		}

		public override void SetData(T value)
		{
			_storage.Value = value;
		}

		public override void Clear()
		{
			_storage.Value = default(T);
		}
	}
}
