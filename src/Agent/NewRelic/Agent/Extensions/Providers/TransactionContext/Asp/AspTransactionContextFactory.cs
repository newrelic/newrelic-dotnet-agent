using System;
using System.Runtime.CompilerServices;
using System.Web;

namespace NewRelic.Agent.Extensions.Providers.TransactionContext
{
    /// <summary>
    /// Factory for creating a AspTransactionContext.
    /// </summary>
    public class AspTransactionContextFactory : IContextStorageFactory
    {
        public bool IsAsyncStorage => false;

        bool IContextStorageFactory.IsValid
        {
            get
            {
                // if attempting to access HttpContext throws an exception then this factory is invalid
                try
                {
                    AccessHttpContextClass();
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }
        }

        ContextStorageType IContextStorageFactory.Type => ContextStorageType.HttpContext;

        IContextStorage<T> IContextStorageFactory.CreateContext<T>(String key)
        {
            return new AspTransactionContext<T>(key);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AccessHttpContextClass()
        {
            if (HttpContext.Current == null)
                return;
        }
    }
}
