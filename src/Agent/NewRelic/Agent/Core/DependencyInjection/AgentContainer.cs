// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Autofac;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.DependencyInjection
{
    public class AgentContainer : IContainer
    {

        private readonly ContainerBuilder _builder;
        private Autofac.IContainer _container;

        // use the scope instead of the container to resolve instances. This allows us to replace registrations in a new scope for unit testing
        private ILifetimeScope _scope;
        private bool _disposedValue;
        private readonly Dictionary<Type, object> _registrationsToReplace = new Dictionary<Type, object>();

        public AgentContainer()
        {
            _builder = new ContainerBuilder();
        }

        public void Build()
        {
            _container = _builder.Build();
            _scope = _container.BeginLifetimeScope();
        }

        public void ReplaceRegistrations()
        {
            // create a new nested scope, registering the requested replacement instances.
            _scope = _scope.BeginLifetimeScope(ReplaceRegistrations);

            _registrationsToReplace.Clear();
        }

        private void ReplaceRegistrations(ContainerBuilder builder)
        {
            foreach (var kvp in _registrationsToReplace)
            {
                builder.RegisterInstance(kvp.Value).As(kvp.Key);
            }
        }


        public void Register<TInterface, TConcrete>()
            where TInterface : class
            where TConcrete : class, TInterface
        {
            _builder.RegisterType<TConcrete>().As<TInterface>().InstancePerLifetimeScope();
        }

        public void Register<TInterface1, TInterface2, TConcrete>()
            where TInterface1 : class
            where TInterface2 : class
            where TConcrete : class, TInterface1, TInterface2
        {
            _builder.RegisterType<TConcrete>().As<TInterface1, TInterface2>().InstancePerLifetimeScope();
        }

        public void RegisterInstance<TInterface>(TInterface instance)
            where TInterface : class
        {
            _builder.RegisterInstance<TInterface>(instance).As<TInterface>().SingleInstance();
        }

        public void RegisterFactory<TInterface>(Func<TInterface> func)
            where TInterface : class
        {
            _builder.Register(c => func.Invoke()).As<TInterface>();
        }

        public void ReplaceInstanceRegistration<TInterface>(TInterface instance)
            where TInterface : class
        {
            // Add this replacement registration to a list, registration actually occurs in ReplaceRegistrations()
            _registrationsToReplace.Add(typeof(TInterface), instance);
        }

        public T Resolve<T>()
        {
            return _scope.Resolve<T>();
        }

        public IEnumerable<T> ResolveAll<T>()
        {
            try
            {
                return _scope.Resolve<IEnumerable<T>>();
            }
            catch (Exception ex)
            {
                Log.Error($"Error during ResolveAll of {typeof(T)}: {ex}");
                throw;
            }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _scope?.Dispose();
                    _container?.Dispose();

                    _scope = null;
                    _container = null;
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
