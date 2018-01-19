using System;
using System.IO;
using System.Threading;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Agent.Core.Utilities;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Events;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Config
{
	/// <inheritdoc />
	/// <summary>
	/// 
	/// </summary>
	public class InstrumentationWatcher : IDisposable
	{
		private const int RequestRejitDelayMilliseconds = 15000;

		private readonly FileSystemWatcher _watcher;
		private readonly INativeMethods _nativeMethods;
		private readonly IWrapperService _wrapperService;
		[NotNull]
		private readonly Subscriptions _subscriptions = new Subscriptions();

		private readonly SignalableAction _action;

		public InstrumentationWatcher(INativeMethods nativeMethods, IWrapperService wrapperService)
		{
			if (AgentInstallConfiguration.HomeExtensionsDirectory == null)
				return;

			_nativeMethods = nativeMethods;
			_wrapperService = wrapperService;
			_subscriptions.Add<ServerConfigurationUpdatedEvent>(OnServerConfigurationUpdated);

			_action = new SignalableAction(RequestRejit, RequestRejitDelayMilliseconds);
			_action.Start();

			_watcher = new FileSystemWatcher();
			_watcher.Path = AgentInstallConfiguration.HomeExtensionsDirectory;
			_watcher.Filter = "*.xml";
			_watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
			_watcher.Changed += OnChanged;
			_watcher.Created += OnChanged;
			_watcher.Deleted += OnChanged;
			_watcher.Renamed += OnRenamed;
			_watcher.EnableRaisingEvents = true;
		}

		private void RequestRejit()
		{
			Log.Info("Requesting ReJIT");
			var result = _nativeMethods.InstrumentationRefresh();
			_wrapperService.ClearCaches();
			Log.InfoFormat("ReJIT request complete: {0}", result);
		}

		private void OnChanged(object sender, FileSystemEventArgs e)
		{
			Log.InfoFormat("Instrumentation change detected: {0} - {1}", e.ChangeType, e.FullPath);
			_action.Signal();
		}

		private void OnRenamed(object sender, RenamedEventArgs e)
		{
			Log.InfoFormat("Instrumentation change detected: {0} - {1} -> {2}", e.ChangeType, e.OldFullPath, e.FullPath);
			_action.Signal();
		}

		public void Dispose()
		{
			_action?.Dispose();
			_watcher?.Dispose();
		}

		private void OnServerConfigurationUpdated([NotNull] ServerConfigurationUpdatedEvent serverConfigurationUpdatedEvent)
		{
			try
			{
				bool receivedInstrumentation = false;
				var instrumentation = serverConfigurationUpdatedEvent.Configuration.Instrumentation;
				if (instrumentation != null)
				{
					foreach (var instrumentationSet in instrumentation)
					{
						receivedInstrumentation = true;
						_nativeMethods.AddCustomInstrumentation(instrumentationSet.Name, instrumentationSet.Config);
					}
				}
				if (receivedInstrumentation)
				{
					Log.InfoFormat("Applying live instrumentation");
				}
				// We want to apply custom instrumentation regardless of whether or not any was received on
				// this connect because we may have received instrumentation on a previous connect.
				_nativeMethods.ApplyCustomInstrumentation();
			}
			catch (Exception ex)
			{
				Log.Error(ex);
			}
		}
	}
}
