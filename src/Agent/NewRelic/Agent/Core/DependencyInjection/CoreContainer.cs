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

		public CoreContainer()
		{
			this.builder = new ContainerBuilder();
		}

		public void Build()
		{
			this.container = builder.Build();
		}

		public void Dispose()
		{
		}

		public void Register<TInterface, TConcrete>()
			where TInterface : class
			where TConcrete : class, TInterface
		{
			builder.RegisterType<TConcrete>().As<TInterface>().SingleInstance();
		}

		public void Register<TInterface1, TInterface2, TConcrete>()
			where TInterface1 : class
			where TInterface2 : class
			where TConcrete : class, TInterface1, TInterface2
		{
			builder.RegisterType<TConcrete>().As<TInterface1, TInterface2>().SingleInstance();
		}

		public void Register<TInterface>(TInterface instance)
			where TInterface : class
		{
			builder.RegisterInstance<TInterface>(instance).As<TInterface>().SingleInstance();
		}

		public void RegisterFactory<TInterface>(Func<TInterface> func)
			where TInterface : class
		{
			builder.Register(c => func.Invoke()).As<TInterface>();
		}

		public void ReplaceRegistration<TInterface>(TInterface instance)
			where TInterface : class
		{
			throw new NotImplementedException();
		}

		public T Resolve<T>()
		{
			Check(typeof(T));
			return container.Resolve<T>();
		}

		public IEnumerable<T> ResolveAll<T>()
		{
			Check(typeof(T));
			try
			{
				return container.Resolve<IEnumerable<T>>();
			} catch (Exception ex)
			{
				Log.Error($"Error during ResolveAll of {typeof(T)}");
				throw ex;
			}
		}
		private void Check(Type type)
		{
			if (container == null)
			{
				throw new Exception("Resolve invoked with uninitialized container for " + type);
			}
		}
	}
}
#endif
