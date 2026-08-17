// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using System.Web.Hosting;
using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.HybridHttpContext;

/// <summary>
/// Hybrid context storage that synchronizes values between HttpContext.Items and an AsyncLocal store.
/// Ensures availability across async/thread hops while still leveraging HttpContext when present.
/// Writes use copy-on-write semantics: a write in one ExecutionContext assigns a new value holder,
/// so it is never visible to that context's parent or siblings.
/// </summary>
public class HybridHttpContextStorage<T>(string key) : IContextStorage<T>
{
    private static readonly AsyncLocal<ValueHolder> _asyncLocal = new();

    private sealed class ValueHolder
    {
        public readonly bool HasValue;
        public readonly T Value;

        public ValueHolder(bool hasValue, T value)
        {
            HasValue = hasValue;
            Value = value;
        }
    }

    public byte Priority => 15; // Higher than HttpContext and AsyncLocal to prefer this storage when available.

    // This storage is only available when running in a hosted web application with HttpContext.
    bool IContextStorage<T>.CanProvide { get { return HostingEnvironment.IsHosted && System.Web.HttpContext.Current != null; } }

    public T GetData()
    {
        var httpCtx = System.Web.HttpContext.Current;
        var holder = _asyncLocal.Value;

        if (httpCtx != null && httpCtx.Items.Contains(key))
        {
            var httpValue = httpCtx.Items[key];
            if (holder == null || !holder.HasValue || !Equals(holder.Value, httpValue))
            {
                _asyncLocal.Value = new ValueHolder(true, (T)httpValue);
            }
            return (T)httpValue;
        }

        if (holder?.HasValue == true)
        {
            if (httpCtx != null && !httpCtx.Items.Contains(key))
            {
                httpCtx.Items[key] = holder.Value; // hydrate HttpContext if available
            }
            return holder.Value;
        }

        return default;
    }

    public void SetData(T value)
    {
        // Assign a new holder rather than mutating the existing one. AsyncLocal isolates
        // contexts by giving each its own reference; mutating the object that reference
        // points to would be visible to every context sharing it -- the parent, siblings,
        // and already-launched children -- which defeats the isolation. Assigning keeps
        // copy-on-write semantics, matching what Clear() already does.
        _asyncLocal.Value = new ValueHolder(true, value);

        var httpCtx = System.Web.HttpContext.Current;
        httpCtx?.Items[key] = value;
    }

    public void Clear()
    {
        var httpCtx = System.Web.HttpContext.Current;
        if (httpCtx != null && httpCtx.Items.Contains(key))
        {
            httpCtx.Items.Remove(key);
        }
        _asyncLocal.Value = new ValueHolder(false, default);
    }
}