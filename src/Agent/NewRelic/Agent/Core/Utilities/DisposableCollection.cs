// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// A collection of disposable objects.  Items will be disposed on removal from this collection.
/// </summary>
public class DisposableCollection : IDisposable, ICollection<IDisposable>
{
    private readonly ICollection<IDisposable> _disposables = new Collection<IDisposable>();

    #region Implementation of IDisposable

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            Debug.Assert(disposable != null);
            disposable.Dispose();
        }

        _disposables.Clear();
    }

    #endregion

    #region Implementation of IEnumerable

    public IEnumerator<IDisposable> GetEnumerator()
    {
        return _disposables.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion

    #region Implementation of ICollection<IDisposable>

    public void Add(IDisposable item)
    {
        if (item == null)
            return;

        _disposables.Add(item);
    }

    public void Clear()
    {
        Dispose();
    }

    public bool Contains(IDisposable item)
    {
        if (item == null)
            return false;

        return _disposables.Contains(item);
    }

    public void CopyTo(IDisposable[] array, int arrayIndex)
    {
        _disposables.CopyTo(array, arrayIndex);
    }

    public bool Remove(IDisposable item)
    {
        if (item == null)
            return false;

        if (!_disposables.Contains(item))
            return false;

        item.Dispose();
        _disposables.Remove(item);
        return true;
    }

    public int Count { get { return _disposables.Count; } }
    public bool IsReadOnly { get { return _disposables.IsReadOnly; } }

    #endregion
}