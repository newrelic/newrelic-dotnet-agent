

using System;

namespace NewRelic.Agent.Extensions.Providers
{
    /// <summary>
    /// Factory for creating an IContextStorage instance.
    /// </summary>
    public class ThreadLocalContextStorageFactory : IContextStorageFactory
    {
        private readonly IThreadLocalFactory _instanceContainerFactory;

        public ThreadLocalContextStorageFactory(IThreadLocalFactory instanceContainerFactory)
        {
            _instanceContainerFactory = instanceContainerFactory;
        }

        public bool IsAsyncStorage => false;

        bool IContextStorageFactory.IsValid => true;

        ContextStorageType IContextStorageFactory.Type => ContextStorageType.ThreadLocal;

        IContextStorage<T> IContextStorageFactory.CreateContext<T>(string key)
        {
            return new ThreadLocalTransactionContext<T>(key, _instanceContainerFactory.Create<T>());
        }

    }
}
