/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.Extensions.Providers
{
    /// <summary>
    /// An instance container is basically a "collection of one".  It holds a reference to a single 
    /// instance.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IInstanceContainer<T>
    {
        T Value { get; set; }
    }

    /// <summary>
    /// This is a thread local implementation of a container that holds a single object instance
    /// for each thread where it is accessed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IThreadLocal<T> : IInstanceContainer<T> { }

    public interface IThreadLocalFactory
    {
        IThreadLocal<T> Create<T>();
    }

    public static class InstanceContainers
    {
        private class ThreadSafeInstanceContainer<T> : IInstanceContainer<T>
        {
            private volatile Func<T> _value;
            public ThreadSafeInstanceContainer(T instance)
            {
                Value = instance;
            }

            public T Value
            {
                get => _value == null ? default(T) : _value.Invoke();
                set
                {
                    _value = value == null ? (Func<T>)null : () => value;
                }
            }
        }
    }
}
