using System.Runtime.Remoting.Messaging;

namespace NewRelic.Providers.CallStack.AsyncLocal
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
            this._key = key;
        }
        public T Value
        {
            get
            {
                var obj = CallContext.LogicalGetData(_key);
                return obj == null ? default(T) : (T)obj;
            }
            set
            {
                CallContext.LogicalSetData(_key, value);
            }
        }
    }
}
