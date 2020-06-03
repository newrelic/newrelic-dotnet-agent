namespace NewRelic.Providers.Storage.CallContext
{
    /// <summary>
    /// A simple implementation of AsyncLocal that works in .NET 4.5.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AsyncLocal<T>
    {
        private readonly string _key;

        public AsyncLocal(string key)
        {
            _key = key;
        }

        public T Value
        {
            get
            {
                var obj = System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(_key);
                return obj == null ? default(T) : (T)obj;
            }
            set
            {
                System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(_key, value);
            }
        }
    }
}
