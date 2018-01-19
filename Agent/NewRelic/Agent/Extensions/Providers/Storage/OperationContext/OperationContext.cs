using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.OperationContext
{
	/// <summary>
	/// ITransactionContext implementation for version 3 of WCF.
	/// </summary>
	public class OperationContext<T> : IContextStorage<T>
	{
		private readonly string _key;

		/// <summary>
		/// 
		/// </summary>
		public OperationContext(string key)
		{
			_key = key;
		}

		byte IContextStorage<T>.Priority { get { return 5; } }

		bool IContextStorage<T>.CanProvide { get { return OperationContextExtension.CanProvide; } }

		T IContextStorage<T>.GetData()
		{
			var currentOperationContext = OperationContextExtension.Current;
			if (currentOperationContext == null)
				return default(T);

			return (T) currentOperationContext.Items[_key];
		}

		void IContextStorage<T>.SetData(T value)
		{
			var currentOperationContext = OperationContextExtension.Current;
			if (currentOperationContext == null)
				return;

			currentOperationContext.Items[_key] = value;
		}

		void IContextStorage<T>.Clear()
		{
			OperationContextExtension.Current?.Items?.Remove(_key);
		}
	}
}
