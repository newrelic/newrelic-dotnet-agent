using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.TypeInstantiation;

namespace NewRelic.Agent.Core.Utilities
{
	public class ExtensionsLoader
	{
		[NotNull]
		public static IEnumerable<T> LoadExtensions<T>()
		{
			var loadPaths = new List<string> { AgentInstallConfiguration.InstallPathExtensionsDirectory };

			if (AgentInstallConfiguration.IsNetstandardPresent)
			{
				loadPaths.Add(AgentInstallConfiguration.InstallPathNetstandardExtensionsDirectory);
			}

			if (AgentInstallConfiguration.IsNet46OrAbovePresent)
			{
				loadPaths.Add(AgentInstallConfiguration.InstallPathNet46ExtensionsDirectory);
			}

			Log.Info($"Loading extensions of type {typeof(T)} from: {string.Join(", ", loadPaths)}");

			var result = TypeInstantiator.ExportedInstancesFromDirectory<T>(loadPaths.ToArray());

			foreach(var ex in result.Exceptions)
			{
				Log.Warn($"An exception occurred while loading an extension: {ex}");
			}

			return result.Instances;
		}

		[NotNull]
		public static IEnumerable<IWrapper> LoadWrappers()
		{
			try
			{
				var wrappers = LoadExtensions<IWrapper>().Where(wrapper => wrapper != null);
				return wrappers;
			} catch (Exception ex)
			{
				Log.Error($"Failed to load wrappers: {ex}");
				throw;
			}
		}

		public static List<IRuntimeInstrumentationGenerator> LoadRuntimeInstrumentationGenerators()
		{
			try
			{
				var runtimeInstrumentationGenerators = LoadExtensions<IRuntimeInstrumentationGenerator>().ToList();
				return runtimeInstrumentationGenerators;
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to load runtime instrumentation generators: {ex}");
				throw;
			}
		}

		/// <summary>
		/// Loads all of the context storage factories.
		/// </summary>
		/// <returns></returns>
		public static IEnumerable<IContextStorageFactory> LoadContextStorageFactories()
		{
			var contextStorageFactories = LoadExtensions<IContextStorageFactory>().ToList();
			if (contextStorageFactories.Count == 0)
			{
				Log.Warn("No context storage factories were loaded from the extensions directory.");
				return new IContextStorageFactory[] { };
			}

			foreach (var factory in contextStorageFactories)
			{
				Log.DebugFormat("Available storage type : {0} ({1})", factory.GetType().FullName, factory.IsValid);
			}

			return contextStorageFactories.Where(IsValid);
		}

		private static bool IsValid(IContextStorageFactory factory)
		{
			try
			{
				return factory != null && factory.IsValid;
			} catch (Exception)
			{
				// REVIEW maybe log at finest?
				return false;
			}
		}
	}
}
