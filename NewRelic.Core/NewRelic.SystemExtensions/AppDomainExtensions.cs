using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace NewRelic.SystemExtensions
{
	public static class AppDomainExtensions
	{
		[NotNull]
		public static IEnumerable<AssemblyName> GetLoadedAssemblyNamesBySimpleName([CanBeNull] this AppDomain appDomain, [CanBeNull] String simpleName)
		{
			if (appDomain == null)
				return Enumerable.Empty<AssemblyName>();
			if (simpleName == null)
				return Enumerable.Empty<AssemblyName>();

			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(assembly => assembly != null)
				.Select(assembly => assembly.GetName())
				.Where(assemblyName => assemblyName != null)
				.Where(assemblyName => assemblyName.Name == simpleName);
		}
#if NET45
		[NotNull]
		public static IEnumerable<String> GetLoadedAssemblyFullNamesBySimpleName([CanBeNull] this AppDomain appDomain, [CanBeNull] String simpleName)
		{
			return appDomain.GetLoadedAssemblyNamesBySimpleName(simpleName)
				.Where(assemblyName => assemblyName != null)
				.Select(assemblyName => assemblyName.FullName);
		}

		public static T CreateInstanceAndUnwrap<T>(this AppDomain appDomain) where T : class
		{
			return appDomain.CreateInstanceAndUnwrap(typeof(T).Assembly.FullName, typeof(T).FullName) as T;
		}

		/// <summary>
		/// Creates a new temporary AppDomain and executes the given action inside of it.
		/// </summary>
		/// <param name="method">The code to execute inside the temporary AppDomain.</param>
		/// <param name="assemblyResolver">Can be null. A static ResolveEventHandler that will be used to resolve assembly load failures.</param>
		/// <param name="inputData">The parameter to be passed to the action if it takes a parameter.</param>
		/// <returns>The return value from the function or null if the function doesn't return anything.</returns>
		public static Object IsolateMethodInAppDomain(Delegate method, ResolveEventHandler assemblyResolver, params Object[] inputData)
		{
			var appDomain = CreateIsolatedDomain(assemblyResolver);
			var crossAppDomainObject = CreateCrossAppDomainObject(appDomain);
			var result = ExecuteAction(crossAppDomainObject, method, inputData);
			AppDomain.Unload(appDomain);
			return result;
		}


		private static AppDomain CreateIsolatedDomain(ResolveEventHandler assemblyResolver)
		{
			var appDomain = AppDomain.CreateDomain("Isolated: " + Guid.NewGuid(), null, new AppDomainSetup
			{
				ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
			});
			if (appDomain == null)
				throw new Exception("Unable to create isolated application domain.");

			if (assemblyResolver != null)
				appDomain.AssemblyResolve += assemblyResolver;

			return appDomain;
		}


		private static CrossAppDomainObject CreateCrossAppDomainObject(AppDomain appDomain)
		{
			var crossAppDomainObject = appDomain.CreateInstanceAndUnwrap<CrossAppDomainObject>();
			if (crossAppDomainObject == null)
				throw new Exception("Unknown failure during app domain creation.");

			return crossAppDomainObject;
		}


		private static Object ExecuteAction(CrossAppDomainObject crossAppDomainObject, Delegate method, params Object[] input)
		{
			crossAppDomainObject.Execute(method, input);

			var exception = crossAppDomainObject.Result as Exception;
			if (exception != null)
				throw exception;

			return crossAppDomainObject.Result;
		}

#endif
		private class CrossAppDomainObject : MarshalByRefObject
		{
			public Object Result { get; private set; }

			public void Execute(Delegate action, params Object[] input)
			{
				try
				{
					Result = action.DynamicInvoke(input);
				}
				catch (TargetInvocationException exception)
				{
					Result = exception.InnerException;
				}
				catch (Exception exception)
				{
					Result = exception;
				}
			}

		}
	}
}
