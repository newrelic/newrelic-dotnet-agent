// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Providers
{
    /// <summary>
    /// Any class that implements this interface will be instantiated using the default constructor during application startup.  A default constructor is required for this context factory to work.
    /// </summary>
    public interface IContextStorageFactory
    {
        /// <summary>
        /// Returns an IContextStorage that will be used to provide a place to store context-specific object instances.
        /// </summary>
        /// <returns>An IContextStorage or null if this factory cannot provide context for this application.</returns>
        /// <remarks>This method will be called once during application startup.  It is safe to do expensive operations here such as class loading or reflection in order to create the context.</remarks>
        IContextStorage<T> CreateContext<T>(string key);

        /// <summary>
        /// Returns true if this context will persist for async execution flow
        /// </summary>
        bool IsAsyncStorage { get; }

        /// <summary>
        /// Returns true if this context uses hybrid storage (for example, HttpContext and AsyncLocal)
        /// </summary>
        bool IsHybridStorage { get; }

        /// Returns true if this context is valid (and possibly throws if it isn't).
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Returns the storage type.
        /// </summary>
        ContextStorageType Type { get; }
    }

    public enum ContextStorageType
    {
        HttpContext,
        OperationContext,
        ThreadLocal,
        CallContextLogicalData,
        AsyncLocal
    }
}
