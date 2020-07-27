
namespace NewRelic.Agent.Extensions.Providers.TransactionContext
{
    /// <summary>
    /// ITransactionContext implementation for version 3 of WCF.
    /// </summary>
    public class Wcf3TransactionContext<T> : IContextStorage<T>
    {
        private readonly string _key;

        /// <summary>
        /// 
        /// </summary>
        public Wcf3TransactionContext(string key)
        {
            _key = key;
        }

        byte IContextStorage<T>.Priority { get { return 5; } }

        bool IContextStorage<T>.CanProvide { get { return Wcf3OperationContextExtension.CanProvide; } }

        T IContextStorage<T>.GetData()
        {
            var currentWcf3OperationContext = Wcf3OperationContextExtension.Current;
            if (currentWcf3OperationContext == null)
                return default(T);

            return (T)currentWcf3OperationContext.Items[_key];
        }

        void IContextStorage<T>.SetData(T value)
        {
            var currentWcf3OperationContext = Wcf3OperationContextExtension.Current;
            if (currentWcf3OperationContext == null)
                return;

            currentWcf3OperationContext.Items[_key] = value;
        }

        void IContextStorage<T>.Clear()
        {
            Wcf3OperationContextExtension.Current?.Items?.Remove(_key);
        }
    }
}
