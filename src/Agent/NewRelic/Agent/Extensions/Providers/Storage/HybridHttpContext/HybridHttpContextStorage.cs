// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.HybridHttpContext
{
    /// <summary>
    /// Hybrid context storage that synchronizes values between HttpContext.Items and an AsyncLocal store.
    /// Ensures availability across async/thread hops while still leveraging HttpContext when present.
    /// </summary>
    public class HybridHttpContextStorage<T>(string key) : IContextStorage<T>
    {
        private static readonly AsyncLocal<ValueHolder> _asyncLocal = new();

        private class ValueHolder { public bool HasValue; public T Value; }

        public byte Priority => 15; // Higher than HttpContext and AsyncLocal to prefer this storage when available.

        // This storage is only available when HttpContext is available.
        bool IContextStorage<T>.CanProvide { get { return System.Web.HttpContext.Current != null; } }

        public T GetData()
        {
            var httpCtx = System.Web.HttpContext.Current;
            var holder = _asyncLocal.Value;

            if (httpCtx != null && httpCtx.Items.Contains(key))
            {
                var httpValue = httpCtx.Items[key];
                if (holder == null || !holder.HasValue || !Equals(holder.Value, httpValue))
                {
                    _asyncLocal.Value = new ValueHolder { HasValue = true, Value = (T)httpValue };
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
            var holder = _asyncLocal.Value;
            if (holder == null)
            {
                _asyncLocal.Value = new ValueHolder { HasValue = true, Value = value };
            }
            else
            {
                holder.HasValue = true;
                holder.Value = value;
            }

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
            _asyncLocal.Value = new ValueHolder { HasValue = false, Value = default };
        }
    }
}
