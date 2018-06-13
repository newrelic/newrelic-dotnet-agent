namespace NewRelic.Agent.Extensions.Providers
{
	/// <summary>
	/// Factory for creating an IContextStorage instance.
	/// </summary>
	public class ThreadLocalContextStorageFactory : IContextStorageFactory
	{
		public bool IsAsyncStorage => false;

		bool IContextStorageFactory.IsValid => true;

		ContextStorageType IContextStorageFactory.Type => ContextStorageType.ThreadLocal;

		IContextStorage<T> IContextStorageFactory.CreateContext<T>(string key)
		{
			return new ThreadLocalStorage<T>(key);
		}
	}
}
