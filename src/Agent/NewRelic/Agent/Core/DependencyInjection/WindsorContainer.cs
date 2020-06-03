#if NET45
using System;
using System.Collections.Generic;
using System.Linq;
using Castle.MicroKernel.ModelBuilder.Inspectors;
using Castle.MicroKernel.Registration;

namespace NewRelic.Agent.Core.DependencyInjection
{
	public class WindsorContainer : IContainer
	{
		private readonly Castle.Windsor.WindsorContainer _windsorContainer;

		public WindsorContainer()
		{
			_windsorContainer = new Castle.Windsor.WindsorContainer();
			
			// Disable property injection
			var propInjector = _windsorContainer.Kernel.ComponentModelBuilder
									 .Contributors
									 .OfType<PropertiesDependenciesModelInspector>()
									 .Single();
			_windsorContainer.Kernel.ComponentModelBuilder.RemoveContributor(propInjector);
		}

		public void Build()
		{
			// no op
		}

		public void Dispose()
		{
			_windsorContainer.Dispose();
		}

		public void Register<TInterface, TConcrete>()
			where TInterface : class
			where TConcrete : class, TInterface
		{
			_windsorContainer.Register(
				Component
					.For<TInterface, TConcrete>()
					.ImplementedBy<TConcrete>()
					.Named(typeof(TInterface).FullName + "-" + typeof(TConcrete).FullName));
		}


		public void Register<TInterface1, TInterface2, TConcrete>()
			where TInterface1 : class
			where TInterface2 : class
			where TConcrete : class, TInterface1, TInterface2
		{
			_windsorContainer.Register(
				Component
					.For<TInterface1, TInterface2, TConcrete>()
					.ImplementedBy<TConcrete>()
					.Named(typeof(TInterface1).FullName + "," + typeof(TInterface2).FullName + "-" + typeof(TConcrete).FullName));
		}

		public void Register<TInterface>(TInterface instance) where TInterface : class
		{
			_windsorContainer.Register(Component.For<TInterface>().Instance(instance));
		}

		public void RegisterFactory<TInterface>(Func<TInterface> func)
			where TInterface : class
		{
			_windsorContainer.Register(Component.For<TInterface>().UsingFactoryMethod(func));
		}

		public void ReplaceRegistration<TInterface>(TInterface instance)
			where TInterface : class
		{
			var guid = Guid.NewGuid().ToString();
			_windsorContainer.Register(Component.For<TInterface>().Instance(instance).Named(guid).IsDefault());
		}

		public T Resolve<T>()
		{
			return _windsorContainer.Resolve<T>();
		}

		public IEnumerable<T> ResolveAll<T>()
		{
			return _windsorContainer.ResolveAll<T>();
		}
	}
}
#endif
