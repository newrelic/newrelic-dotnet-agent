using System;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.AsyncProviders
{
    public class AsyncTransactionContextFactory : IContextStorageFactory
    {
        public bool IsAsyncStorage => true;

        public String Name => GetType().FullName;

        public bool IsValid => true;

        public ContextStorageType Type => ContextStorageType.AsyncLocal;

        public IContextStorage<T> CreateContext<T>(string key)
        {
            return new AsyncTransactionContext<T>();
        }
    }
}
