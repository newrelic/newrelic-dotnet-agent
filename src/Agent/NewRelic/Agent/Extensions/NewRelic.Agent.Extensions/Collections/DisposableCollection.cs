// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.ObjectModel;

namespace NewRelic.Agent.Extensions.Collections
{
    public class DisposableCollection : DisposableCollection<IDisposable> { }

    /// <summary>
    /// A collection of disposable objects.  Items will be disposed on removal from this collection.
    /// </summary>
    public class DisposableCollection<T> : Collection<T>, IDisposable where T : IDisposable
    {
        public void Dispose()
        {
            Clear();
        }

        protected override void InsertItem(int index, T disposable)
        {
            if (disposable == null) return;
            base.InsertItem(index, disposable);
        }

        protected override void RemoveItem(int index)
        {
            Items[index].Dispose();
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            foreach (var disposable in this)
            {
                disposable.Dispose();
            }
            base.ClearItems();
        }

    }
}
