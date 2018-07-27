using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web.Mvc;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Mvc3
{
	internal static class AsyncActionInstrumentor
	{
		public static List<InstrumentationPoint> GetInstrumentation()
		{
			var instrumentationPoints = new List<InstrumentationPoint>();
			var asyncControllerMethods = new List<MethodInfo>();

			var assemblies = AppDomain.CurrentDomain.GetAssemblies();

			var filteredAssemblies = assemblies
				.Where(x => !x.FullName.StartsWith("System.") && 
				            !x.FullName.StartsWith("Microsoft.") && 
				            !x.FullName.StartsWith("NewRelic."))
				.ToList();

			foreach (var assembly in filteredAssemblies)
			{
				Type[] types;

				try
				{
					types = assembly.GetTypes();
				}
				catch (ReflectionTypeLoadException ex)
				{
					types = ex.Types;
				}

				var methods = types
					.Where(type => type != null && typeof(Controller).IsAssignableFrom(type))
					.SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public))
					.Where(method => !method.IsDefined(typeof(NonActionAttribute)) && method.IsDefined(typeof(AsyncStateMachineAttribute)))
					.ToList();

				asyncControllerMethods.AddRange(methods);
			}

			foreach (var methodInfo in asyncControllerMethods)
			{
				var point = new InstrumentationPoint(methodInfo, "AttachToAsyncWrapper");
				instrumentationPoints.Add(point);
			}

			return instrumentationPoints;
		}
	}
}