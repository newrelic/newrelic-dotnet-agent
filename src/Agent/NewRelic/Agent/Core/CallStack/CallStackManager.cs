using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.CallStack
{
    public interface ICallStackManager
    {
        /// <summary>
        /// Adds a new object to the top of the callstack.
        /// </summary>
        void Push(int uniqueId);

        /// <summary>
        /// Removes the given object from top of the callstack. Does nothing if the stack is callstack empty. Will throw if the callstack is not empty and the given object is not on top.
        /// </summary>
        void TryPop(int uniqueId, int? parentId);

        /// <summary>
        /// Returns the object on top of the callstack, or null if callstack is empty.
        /// </summary>
        /// <returns>The object on top of the callstack, or null if callstack is empty.</returns>
        int? TryPeek();

        /// <summary>
        /// Clears the callstack.
        /// </summary>
        void Clear();

        /// <summary>
        /// Switches from synchronous to asynchronous storage.
        /// </summary>
        /// <returns>Returns true if the storage mechanism was switched.</returns>
        bool AttachToAsync();
    }

    public delegate void CallStackPop(Object uniqueObject, Object uniqueParent);

    public interface ICallStackManagerFactory
    {
        ICallStackManager CreateCallStackManager();
    }

    public class ResolvedCallStackManagerFactory : ICallStackManagerFactory
    {
        private readonly ICallStackManagerFactory _callStackManagerFactory;

        public ResolvedCallStackManagerFactory(IEnumerable<IContextStorageFactory> storageFactories)
        {
            _callStackManagerFactory = CreateFactory(storageFactories);
        }

        private static ICallStackManagerFactory CreateFactory(IEnumerable<IContextStorageFactory> storageFactories)
        {
            foreach (var factory in storageFactories)
            {
                if (factory.Type == ContextStorageType.AsyncLocal)
                {
                    Log.DebugFormat("Using async storage {0} for call stack", factory.GetType().FullName);
                    return new AsyncCallStackManagerFactory(factory);
                }
                else if (factory.Type == ContextStorageType.CallContextLogicalData)
                {
                    Log.DebugFormat("Using async storage {0} for call stack", factory.GetType().FullName);
                    return new AsyncCallStackManagerFactory(factory);
                }
            }

            var list = storageFactories.ToList();
            list.Add(GetThreadLocalContextStorageFactory());

            return new CallStackManagerFactory(list);
        }

        public ICallStackManager CreateCallStackManager()
        {
            return _callStackManagerFactory.CreateCallStackManager();
        }

        private static IContextStorageFactory GetThreadLocalContextStorageFactory()
        {
            return new ThreadLocalContextStorageFactory(new ThreadLocalFactory());
        }

        private class ThreadLocalFactory : IThreadLocalFactory
        {
            public IThreadLocal<T> Create<T>()
            {
                return new ConcurrentDictionaryThreadLocal<T>();
            }
        }

        private class ConcurrentDictionaryThreadLocal<T> : IThreadLocal<T>
        {
            [NotNull]
            private readonly IDictionary<int, T> _threadToItem = new ConcurrentDictionary<int, T>();

            public T Value
            {
                get
                {
                    var id = Thread.CurrentThread.ManagedThreadId;
                    if (_threadToItem.TryGetValue(id, out T value))
                    {
                        return value;
                    }
                    return default(T);
                }

                set
                {
                    var id = Thread.CurrentThread.ManagedThreadId;
                    if (value == null)
                    {
                        _threadToItem.Remove(id);
                    }
                    else
                    {
                        _threadToItem[id] = value;
                    }
                }
            }
        }
    }

    public class CallStackManagerFactory : ICallStackManagerFactory
    {
        private readonly IEnumerable<IContextStorageFactory> _storageFactories;

        public CallStackManagerFactory(IEnumerable<IContextStorageFactory> storageFactories)
        {
            this._storageFactories = storageFactories;
        }

        public ICallStackManager CreateCallStackManager()
        {
            // we don't know yet which of these CanProvide, but we know which can load their assemblies.
            // defer final decision on which one to use to the CallStackTracker
            var parentTrackers = _storageFactories
                    .Select(factory => factory.CreateContext<int?>("NewRelic.ParentObject"))
                    .Where(tracker => tracker != null)
                    .OrderByDescending(context => context.Priority)
                    .ToList();

            // even though the ASP and WCF contexts can't provide at this point, the Default context can.
            // Want to make sure it is there (.dll could have been deleted from \Extensions), else fall back.
            return new CallStackManager(parentTrackers);
        }
    }

    public class AsyncCallStackManagerFactory : ICallStackManagerFactory
    {
        private readonly IContextStorage<int?> _storageContext;

        public AsyncCallStackManagerFactory(IContextStorageFactory factory)
        {
            this._storageContext = factory.CreateContext<int?>("NewRelic.ParentObject");
        }

        public ICallStackManager CreateCallStackManager()
        {
            // our async contexts can exhibit undesirable sticky behavior if they're first accessed in
            // a synchronous context.  This clears out anything stuck on the context.
            _storageContext.Clear();
            return new SyncToAsyncCallStackManager(_storageContext);
        }
    }

    /// <summary>
    /// A call stack manager that starts synchronous and switches to async storage when AttachToAsync
    /// is called.
    /// </summary>
    public class SyncToAsyncCallStackManager : BaseCallStackManager
    {
        private readonly IContextStorage<int?> _asyncContextStorage;
        private volatile IContextStorage<int?> _currentContextStorage;

        public SyncToAsyncCallStackManager(IContextStorage<int?> asyncContextStorage)
        {
            _asyncContextStorage = asyncContextStorage;
            _currentContextStorage = new SynchronousContextStorage();
        }
        public override bool AttachToAsync()
        {
            var currentValue = _currentContextStorage.GetData();
            _currentContextStorage = _asyncContextStorage;
            _currentContextStorage.SetData(currentValue);
            return true;
        }

        protected override IContextStorage<int?> CurrentStorage => _currentContextStorage;

        private sealed class SynchronousContextStorage : IContextStorage<int?>
        {
            private int? _id;
            public byte Priority => 0;

            public bool CanProvide => true;

            public void Clear()
            {
                _id = null;
            }

            public int? GetData()
            {
                return _id;
            }

            public void SetData(int? id)
            {
                _id = id;
            }
        }
    }

    public class CallStackManager : BaseCallStackManager
    {
        [NotNull]
        private readonly IEnumerable<IContextStorage<int?>> _parentTrackers;

        public CallStackManager(List<IContextStorage<int?>> parentTrackers)
        {
            this._parentTrackers = parentTrackers;
        }

        protected override IContextStorage<int?> CurrentStorage
        {
            get
            {
                foreach (var storage in _parentTrackers)
                {
                    if (storage.CanProvide)
                    {
                        return storage;
                    }
                }
                return null;
            }
        }
    }

    public abstract class BaseCallStackManager : ICallStackManager
    {
        protected abstract IContextStorage<int?> CurrentStorage { get; }

        public void Push(int id)
        {
            CurrentStorage?.SetData(id);
        }

        public void TryPop(int uniqueId, int? parentId)
        {
            var storage = CurrentStorage;
            // It is OK to ignore pops when the intended object is not on top of call stack. There are several non-exceptional scenarios where this occurs, particularly for async code.
            if (storage?.GetData() != uniqueId)
                return;

            storage?.SetData(parentId);
        }

        public int? TryPeek()
        {
            return CurrentStorage?.GetData();
        }

        public void Clear()
        {
            CurrentStorage?.SetData(null);
        }

        public virtual bool AttachToAsync()
        {
            return false;
        }
    }
}
