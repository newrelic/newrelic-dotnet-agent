using NewRelic.Agent.Extensions.Providers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using NewRelic.TypeInstantiation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NewRelic.Agent.Core.Utilities
{
	public class ExtensionsLoader
	{
		/// <summary>
		/// The list of wrappers and the filepath of the assembly where they can be found
		/// </summary>
		private static Dictionary<string, string> _dynamicLoadWrapperAssemblies = new Dictionary<string, string>();

		private static string _installPathExtensionsDirectory;

		/// <summary>
		/// These assemblies are automatically autoreflected upon agent startup.
		/// </summary>
		private static string[] _autoReflectedAssemblies;

		public static void Initialize(string installPathExtensionsDirectory)
		{
			_installPathExtensionsDirectory = installPathExtensionsDirectory;

			_dynamicLoadWrapperAssemblies = new Dictionary<string, string>() {
				{ "BuildCommonServicesWrapper",																		Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.AspNetCore.dll") },
				{ "GenericHostWebHostBuilderExtensionsWrapper",														Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.AspNetCore.dll") },
				{ "NewRelic.Providers.Wrapper.AspNetCore.InvokeActionMethodAsync",									Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.AspNetCore.dll") },

				{ "ResolveAppWrapper",																				Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Owin.dll") },

				{ "Asp35.CreateEventExecutionStepsTracer",															Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Asp35.dll") },
				{ "Asp35.ExecuteStepTracer",																		Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Asp35.dll") },
				{ "Asp35.FinishPipelineRequestTracer",																Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Asp35.dll") },
				{ "Asp35.OnErrorTracer",																			Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Asp35.dll") },
				{ "Asp35.GetRouteDataTracer",																		Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Asp35.dll") },
				{ "Asp35.CallHandlerTracer",																		Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Asp35.dll") },
				{ "Asp35.FilterTracer",																				Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Asp35.dll") },
				{ "Asp35.AspPagesTransactionNameTracer",															Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Asp35.dll") },

				{ "OdbcCommandTracer",																				Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },
				{ "OleDbCommandTracer",																				Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },

				{ "SqlCommandTracer",																				Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },
				{ "SqlCommandTracerAsync",																			Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },
				{ "SqlCommandWrapper",																				Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },
				{ "SqlCommandWrapperAsync",																			Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },

				{ "DataReaderTracer",																				Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },
				{ "DataReaderTracerAsync",																			Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },
				{ "DataReaderWrapper",																				Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },
				{ "DataReaderWrapperAsync",																			Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },

				{ "OpenConnectionTracer",																			Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },
				{ "OpenConnectionWrapper",																			Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Wrapper.Sql.dll") },

			};

			var nonAutoReflectedAssemblies = _dynamicLoadWrapperAssemblies.Values.Distinct().ToList();

			//Add this to the log.
			nonAutoReflectedAssemblies.ForEach(x =>
			{
				Log.Info($"The following assembly will be loaded on-demand: {x}");
			});

			if (!AgentInstallConfiguration.IsNet46OrAbovePresent)
			{
				var asmPath = Path.Combine(_installPathExtensionsDirectory, "NewRelic.Providers.Storage.AsyncLocal.dll");
				Log.Info($"The following assembly is not applicable based on installed Framework: {asmPath}");

				nonAutoReflectedAssemblies.Add(asmPath);
			}

			var assemblyFiles = GetAssemblyFilesFromFolder(_installPathExtensionsDirectory);

			//remove assemblies that are not 
			_autoReflectedAssemblies = assemblyFiles
				.Where(x => !nonAutoReflectedAssemblies.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
		}

		/// <summary>
		/// A list of dynamically loaded assemblies and whether or not they've been loaded
		/// </summary>
		private static Dictionary<string, IWrapper[]> _dynamicLoadAssemblyStatus = new Dictionary<string, IWrapper[]>();


		/// <summary>
		/// Automatically inspect assemblies in the install path and load/instantiate all items of type T.
		/// This method will exclude NotAutoReflected assemblies.  These assemblies are only loaded on-demand so as
		/// to avoid TypeLoad exceptions from missing types.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		private static IEnumerable<T> AutoLoadExtensions<T>()
		{
			Log.Info($"Loading extensions of type {typeof(T)} from folder: {_installPathExtensionsDirectory}");

			var result = TypeInstantiator.ExportedInstancesFromAssemblyPaths<T>(_autoReflectedAssemblies);

			foreach (var ex in result.Exceptions)
			{
				Log.Warn($"An exception occurred while loading an extension: {ex}");
			}

			return result.Instances;
		}

		public static IEnumerable<IWrapper> LoadWrappers()
		{
			try
			{
				//Load all wrappers, except for the ones that are loaded upon their first usage (dynamic wrappers).
				//This avoids TypeLoad exceptions for unsupported types contained in those wrappers
				var wrappers = AutoLoadExtensions<IWrapper>()
					.Where(wrapper => wrapper != null);

				return wrappers;
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to load wrappers: {ex}");
				throw;
			}
		}

		public static List<IRuntimeInstrumentationGenerator> LoadRuntimeInstrumentationGenerators()
		{
			try
			{
				var runtimeInstrumentationGenerators = AutoLoadExtensions<IRuntimeInstrumentationGenerator>().ToList();
				return runtimeInstrumentationGenerators;
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to load runtime instrumentation generators: {ex}");
				throw;
			}
		}

		private static List<string> GetAssemblyFilesFromFolder(string folder)
		{
			if (folder == null || !Directory.Exists(folder))
			{
				return new List<string>();
			}

			var assemblyPaths = Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly);

			return assemblyPaths.ToList();
		}

		/// <summary>
		/// Loads all of the context storage factories.
		/// </summary>
		/// <returns></returns>
		public static IEnumerable<IContextStorageFactory> LoadContextStorageFactories()
		{
			var contextStorageFactories = AutoLoadExtensions<IContextStorageFactory>().ToList();

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

		private static object _loadDynamicWrapperLockObj = new object();

		public static IEnumerable<IWrapper> LoadDynamicWrapper(string assemblyPath)
		{

			lock (_loadDynamicWrapperLockObj)
			{
				if (!_dynamicLoadAssemblyStatus.TryGetValue(assemblyPath.ToLower(), out var wrappers))
				{
					var result = TypeInstantiator.ExportedInstancesFromAssemblyPaths<IWrapper>(assemblyPath);

					foreach (var ex in result.Exceptions)
					{
						Log.Warn($"An exception occurred while loading an extension: {ex}");
					}

					wrappers = result.Instances.ToArray();

					_dynamicLoadAssemblyStatus[assemblyPath.ToLower()] = wrappers;
				}

				return wrappers;
			}


		}

		public static IEnumerable<IWrapper> TryGetDynamicWrapperInstance(string requestedWrapperName)
		{
			if (!_dynamicLoadWrapperAssemblies.TryGetValue(requestedWrapperName, out var assemblyPath))
			{
				return Enumerable.Empty<IWrapper>();
			}

			return LoadDynamicWrapper(assemblyPath);
		}


		private static bool IsValid(IContextStorageFactory factory)
		{
			try
			{
				return factory != null && factory.IsValid;
			}
			catch (Exception)
			{
				// REVIEW maybe log at finest?
				return false;
			}
		}
	}
}