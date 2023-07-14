// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_0
using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Logging;

using System.Linq;
using System.Text;
using Autofac;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.DependencyInjection
{
    public class CoreContainer : IContainer
    {

        private readonly ContainerBuilder builder;
        private Autofac.IContainer container;

        // use the scope instead of the container to resolve instances. This allows us to replace registrations in a new scope for unit testing
        private ILifetimeScope scope;
        private Dictionary<Type, object> _registrationsToReplace = new Dictionary<Type, object>();

        public CoreContainer()
        {
            this.builder = new ContainerBuilder();
        }

        public void Build()
        {
            this.container = builder.Build();
            scope = this.container.BeginLifetimeScope();
        }

        public void ReplaceRegistrations()
        {
            // create a new nested scope, registering the requested replacement instances
            scope = scope.BeginLifetimeScope(ReplaceRegistrations);

            _registrationsToReplace.Clear();
        }

        private void ReplaceRegistrations(ContainerBuilder builder)
        {
            foreach (var kvp in _registrationsToReplace)
            {
                builder.RegisterInstance(kvp.Value).As(kvp.Key);
            }
        }

        public void Dispose()
        {
            scope?.Dispose();
            container?.Dispose();
        }

        public void Register<TInterface, TConcrete>()
            where TInterface : class
            where TConcrete : class, TInterface
        {
            builder.RegisterType<TConcrete>().As<TInterface>().InstancePerLifetimeScope();
        }

        public void Register<TInterface1, TInterface2, TConcrete>()
            where TInterface1 : class
            where TInterface2 : class
            where TConcrete : class, TInterface1, TInterface2
        {
            builder.RegisterType<TConcrete>().As<TInterface1, TInterface2>().InstancePerLifetimeScope();
        }

        public void RegisterInstance<TInterface>(TInterface instance)
            where TInterface : class
        {
            builder.RegisterInstance<TInterface>(instance).As<TInterface>().SingleInstance();
        }

        public void RegisterFactory<TInterface>(Func<TInterface> func)
            where TInterface : class
        {
            builder.Register(c => func.Invoke()).As<TInterface>();
        }

        public void ReplaceInstanceRegistration<TInterface>(TInterface instance)
            where TInterface : class
        {
            // Add this replacement registration to a list, registration actually occurs in ReplaceRegistrations()
            _registrationsToReplace.Add(typeof(TInterface), instance);
        }

        public T Resolve<T>()
        {
            Check(typeof(T));
            return scope.Resolve<T>();
        }

        public IEnumerable<T> ResolveAll<T>()
        {
            Check(typeof(T));
            try
            {
                return scope.Resolve<IEnumerable<T>>();
            }
            catch (Exception ex)
            {
                Log.Error($"Error during ResolveAll of {typeof(T)}");
                throw ex;
            }
        }
        private void Check(Type type)
        {
            if (scope == null)
            {
                throw new Exception("Resolve invoked with uninitialized container for " + type);
            }
        }
    }
}
#endif
