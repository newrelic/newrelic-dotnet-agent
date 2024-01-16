// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Reflection;

namespace NewRelic.TypeInstantiation.UnitTests
{
	public static class AppDomainExtensions
	{
		/// <summary>
		/// Creates a new temporary AppDomain and executes the given action inside of it.
		/// </summary>
		/// <param name="method">The code to execute inside the temporary AppDomain.</param>
		/// <param name="assemblyResolver">Can be null. A static ResolveEventHandler that will be used to resolve assembly load failures.</param>
		/// <param name="inputData">The parameter to be passed to the action if it takes a parameter.</param>
		/// <returns>The return value from the function or null if the function doesn't return anything.</returns>
		public static object IsolateMethodInAppDomain(Delegate method, params object[] inputData)
		{
			var appDomain = CreateIsolatedDomain();
			var crossAppDomainObject = CreateCrossAppDomainObject(appDomain);
			var result = ExecuteAction(crossAppDomainObject, method, inputData);
			AppDomain.Unload(appDomain);
			return result;
		}


		private static AppDomain CreateIsolatedDomain()
		{
			var appDomain = AppDomain.CreateDomain("Isolated: " + Guid.NewGuid(), null, new AppDomainSetup
			{
				ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
			});
			if (appDomain == null)
				throw new Exception("Unable to create isolated application domain.");

			return appDomain;
		}


		private static CrossAppDomainObject CreateCrossAppDomainObject(AppDomain appDomain)
		{
			var crossAppDomainObject = (CrossAppDomainObject) appDomain.CreateInstanceAndUnwrap(typeof(CrossAppDomainObject).Assembly.FullName, typeof(CrossAppDomainObject).FullName);
			if (crossAppDomainObject == null)
				throw new Exception("Unknown failure during app domain creation.");

			return crossAppDomainObject;
		}


		private static object ExecuteAction(CrossAppDomainObject crossAppDomainObject, Delegate method, params object[] input)
		{
			crossAppDomainObject.Execute(method, input);

			var exception = crossAppDomainObject.Result as Exception;
			if (exception != null)
				throw exception;

			return crossAppDomainObject.Result;
		}

		private class CrossAppDomainObject : MarshalByRefObject
		{
			public object Result { get; private set; }

			public void Execute(Delegate action, params object[] input)
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
#endif
