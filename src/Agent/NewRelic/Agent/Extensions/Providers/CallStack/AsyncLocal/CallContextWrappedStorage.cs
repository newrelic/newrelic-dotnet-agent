using JetBrains.Annotations;

namespace NewRelic.Providers.CallStack.AsyncLocal
{
	public class CallContextWrappedStorage<T> : CallContextStorageBase<T>
	{
		[NotNull]
		private readonly AsyncLocal<MarshalByRefContainer> _storage;

		public CallContextWrappedStorage(string key)
		{
			_storage = new AsyncLocal<MarshalByRefContainer>(key);
		}

		public override T GetData()
		{
			return (_storage.Value == null) ? default(T) : (T) _storage.Value.GetValue();
		}

		public override void SetData(T value)
		{
			_storage.Value = new MarshalByRefContainer(value);
		}

		public override void Clear()
		{
			_storage.Value = null;
		}
	}
}
