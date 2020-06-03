using NewRelic.Agent.Extensions.Providers;
using System;

namespace NewRelic.Providers.Storage.AsyncLocal
{
    public class AsyncLocalStorageFactory : IContextStorageFactory
    {
        public bool IsAsyncStorage => true;

        public string Name => GetType().FullName;

        public bool IsValid => true;

        public ContextStorageType Type => ContextStorageType.AsyncLocal;

        public IContextStorage<T> CreateContext<T>(string key)
        {
            return new AsyncLocalStorage<T>();
        }
    }
}
