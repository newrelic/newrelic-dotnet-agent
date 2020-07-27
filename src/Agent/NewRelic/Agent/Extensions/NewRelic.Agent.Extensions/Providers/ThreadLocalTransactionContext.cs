using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using JetBrains.Annotations;

namespace NewRelic.Agent.Extensions.Providers
{
    /// <summary>
    /// A general use transaction context backed by a thread static variable.  Will work well whenever transactions are single threaded and and uninterupted.
    /// </summary>
    public class ThreadLocalTransactionContext<T> : IContextStorage<T>
    {

        /// <summary>
        /// Well, shit.  TheadLocal<T> is available in .NET 4.0 and we're supporting 3.5.
        /// This is our poor man's thread local.  We want the storage to be specific to each
        /// instance of this class so we can't use ThreadStatic.
        /// 
        /// One thing to note is that while there will be contention on the lock in this instance
        /// for Transaction storage (since we have a globally used instance of this object to track
        /// those), there will be almost no contention on the locks for the call stacks, since
        /// an instance of this storage type will be created for each transaction.
        /// </summary>
        [NotNull]
        private readonly IThreadLocal<T> _threadLocal;

        /// <summary>
        /// 
        /// </summary>
        public ThreadLocalTransactionContext(String key, IThreadLocal<T> threadLocal)
        {
            _threadLocal = threadLocal;
        }

        byte IContextStorage<T>.Priority { get { return 1; } }
        bool IContextStorage<T>.CanProvide { get { return true; } }

        T IContextStorage<T>.GetData()
        {
            return _threadLocal.Value;
        }

        void IContextStorage<T>.SetData(T value)
        {
            _threadLocal.Value = value;
        }

        void IContextStorage<T>.Clear()
        {
            _threadLocal.Value = default(T);
        }
    }
}
