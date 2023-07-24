// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.DependencyInjection
{
    public interface IContainer : IDisposable
    {
        void Register<TInterface, TConcrete>()
            where TInterface : class
            where TConcrete : class, TInterface;

        void Register<TInterface1, TInterface2, TConcrete>()
            where TInterface1 : class
            where TInterface2 : class
            where TConcrete : class, TInterface1, TInterface2;

        void RegisterInstance<TInterface>(TInterface instance)
            where TInterface : class;

        void RegisterFactory<TInterface>(Func<TInterface> func)
            where TInterface : class;

        void ReplaceInstanceRegistration<TInterface>(TInterface instance)
            where TInterface : class;

        T Resolve<T>();

        IEnumerable<T> ResolveAll<T>();

        void Build();

        void ReplaceRegistrations();
    }
}
