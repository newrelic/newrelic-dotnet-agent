// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Agent.Core.CallStack;

public interface ICallStackManager
{
    /// <summary>
    /// Adds a new object to the top of the callstack.
    /// </summary>
    void Push(int uniqueId);

    /// <summary>
    /// Removes the given object from top of the callstack. Does nothing if the stack is callstack empty or the given object is not on top.
    /// </summary>
    void TryPop(int uniqueId, int? parentId);

    /// <summary>
    /// Returns the object on top of the callstack, or null if callstack is empty.
    /// </summary>
    /// <returns>The object on top of the callstack, or null if callstack is empty.</returns>
    int? TryPeek();

    /// <summary>
    /// Removes this manager's own entry from the callstack and restores any entry it displaced.
    /// </summary>
    void Clear();

    /// <summary>
    /// Switches from synchronous to asynchronous storage.
    /// </summary>
    /// <returns>Returns true if the storage mechanism was switched.</returns>
    bool AttachToAsync();
}

/// <summary>
/// A parent segment id tagged with the call stack manager (one per transaction) that wrote it.
/// </summary>
[NeedSerializableContainer]
public sealed class CallStackEntry
{
    public CallStackEntry(ICallStackManager owner, int id, CallStackEntry displaced)
    {
        Owner = owner;
        Id = id;
        Displaced = displaced;
        DisplacedDepth = displaced == null ? 0 : displaced.DisplacedDepth + 1;
    }

    public ICallStackManager Owner { get; }

    public int Id { get; }

    /// <summary>
    /// The other manager's entry that this manager's push covered. It is restored when this manager's root entry is popped.
    /// </summary>
    public CallStackEntry Displaced { get; }

    /// <summary>
    /// The number of entries in the Displaced chain under this entry.
    /// </summary>
    public int DisplacedDepth { get; }
}

public delegate void CallStackPop(object uniqueObject, object uniqueParent);

public interface ICallStackManagerFactory
{
    ICallStackManager CreateCallStackManager();
}

public class ResolvedCallStackManagerFactory : ICallStackManagerFactory
{
    private readonly ICallStackManagerFactory _callStackManagerFactory;
    private readonly IConfiguration _configuration;

    public ResolvedCallStackManagerFactory(IEnumerable<IContextStorageFactory> storageFactories, IConfigurationService configurationService)    
    {
        _configuration = configurationService.Configuration;
        _callStackManagerFactory = CreateFactory(storageFactories);
    }

    private ICallStackManagerFactory CreateFactory(IEnumerable<IContextStorageFactory> storageFactories)
    {
        var listOfFactories = storageFactories.ToList();

        // if hybrid http context storage is enabled, use it instead of asynclocal but fallback to asynclocal if it's not available
        var asyncLocalFactory = 
            listOfFactories.FirstOrDefault(f => _configuration.HybridHttpContextStorageEnabled  && f.IsHybridStorage)
            ??
            listOfFactories.FirstOrDefault(f => f.Type == ContextStorageType.AsyncLocal);

        if (asyncLocalFactory != null)
        {
            Log.Debug("Using async storage {0} for call stack with AsyncCallStackManagerFactory", asyncLocalFactory.GetType().FullName);
            return new AsyncCallStackManagerFactory(asyncLocalFactory);
        }

        var callContextLogicalDataFactory = listOfFactories.FirstOrDefault(f => f.Type == ContextStorageType.CallContextLogicalData);
        if (callContextLogicalDataFactory != null)
        {
            Log.Debug("Using async storage {0} for call stack with AsyncCallStackManagerFactory", callContextLogicalDataFactory.GetType().FullName);
            return new AsyncCallStackManagerFactory(callContextLogicalDataFactory);
        }

        listOfFactories.Add(GetThreadLocalContextStorageFactory());

        Log.Debug("No specialized async storage found. Using standard factories with CallStackManagerFactory.");
        return new CallStackManagerFactory(listOfFactories);
    }

    public ICallStackManager CreateCallStackManager()
    {
        return _callStackManagerFactory.CreateCallStackManager();
    }

    private static IContextStorageFactory GetThreadLocalContextStorageFactory()
    {
        return new ThreadLocalContextStorageFactory();
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
            .Select(factory => factory.CreateContext<CallStackEntry>("NewRelic.ParentObject"))
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
    private readonly IContextStorage<CallStackEntry> _storageContext;

    public AsyncCallStackManagerFactory(IContextStorageFactory factory)
    {
        this._storageContext = factory.CreateContext<CallStackEntry>("NewRelic.ParentObject");
    }

    public ICallStackManager CreateCallStackManager()
    {
        return new SyncToAsyncCallStackManager(_storageContext);
    }
}

/// <summary>
/// A call stack manager that starts synchronous and switches to async storage when AttachToAsync
/// is called.
/// </summary>
public class SyncToAsyncCallStackManager : BaseCallStackManager
{
    private readonly IContextStorage<CallStackEntry> _asyncContextStorage;
    private volatile bool _isAttached;
    private int? _synchronousId;

    public SyncToAsyncCallStackManager(IContextStorage<CallStackEntry> asyncContextStorage)
    {
        _asyncContextStorage = asyncContextStorage;
    }

    protected override IContextStorage<CallStackEntry> CurrentStorage => _asyncContextStorage;

    public override bool AttachToAsync()
    {
        if (_isAttached)
            return true;

        // Do not write null: another transaction's entry in this flow must stay in place.
        if (_synchronousId.HasValue)
            base.Push(_synchronousId.Value);

        _isAttached = true;
        return true;
    }

    public override void Push(int id)
    {
        if (_isAttached)
            base.Push(id);
        else
            _synchronousId = id;
    }

    public override void TryPop(int uniqueId, int? parentId)
    {
        if (_isAttached)
            base.TryPop(uniqueId, parentId);
        else if (_synchronousId == uniqueId)
            _synchronousId = parentId;
    }

    public override int? TryPeek()
    {
        return _isAttached ? base.TryPeek() : _synchronousId;
    }

    public override void Clear()
    {
        if (_isAttached)
            base.Clear();
        else
            _synchronousId = null;
    }
}

public class CallStackManager : BaseCallStackManager
{
    private readonly IEnumerable<IContextStorage<CallStackEntry>> _parentTrackers;

    public CallStackManager(List<IContextStorage<CallStackEntry>> parentTrackers)
    {
        this._parentTrackers = parentTrackers;
    }

    protected override IContextStorage<CallStackEntry> CurrentStorage
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

/// <summary>
/// Reads and writes a shared parent slot, ignoring entries that another manager wrote.
/// </summary>
public abstract class BaseCallStackManager : ICallStackManager
{
    private const int MaxDisplacedDepth = 8;

    protected abstract IContextStorage<CallStackEntry> CurrentStorage { get; }

    public virtual void Push(int id)
    {
        var storage = CurrentStorage;
        if (storage == null)
            return;

        // Keep the other manager's entry under this one; an own entry passes on what it already covers.
        var current = storage.GetData();
        var displaced = current != null && current.Owner == this ? current.Displaced : current;
        // Cap the chain so entries leaked in a long-lived flow cannot grow it without bound.
        if (displaced != null && displaced.DisplacedDepth >= MaxDisplacedDepth)
            displaced = null;
        storage.SetData(new CallStackEntry(this, id, displaced));
    }

    public virtual void TryPop(int uniqueId, int? parentId)
    {
        var storage = CurrentStorage;
        var entry = storage?.GetData();
        // It is OK to ignore pops when the intended object is not on top of call stack. There are several non-exceptional scenarios where this occurs, particularly for async code.
        if (entry == null || entry.Owner != this || entry.Id != uniqueId)
            return;

        storage.SetData(parentId.HasValue ? new CallStackEntry(this, parentId.Value, entry.Displaced) : entry.Displaced);
    }

    public virtual int? TryPeek()
    {
        var entry = CurrentStorage?.GetData();
        return entry != null && entry.Owner == this ? entry.Id : null;
    }

    public virtual void Clear()
    {
        var storage = CurrentStorage;
        var entry = storage?.GetData();
        if (entry != null && entry.Owner == this)
            storage.SetData(entry.Displaced);
    }

    public virtual bool AttachToAsync()
    {
        return false;
    }
}
