using System;
using System.IO;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Events;

namespace NewRelic.Agent.Core.Config
{
	/// <inheritdoc />
	/// <summary>
	/// 
	/// </summary>
	public class InstrumentationWatcher : ConfigurationBasedService
	{
		private const int RequestRejitDelayMilliseconds = 15000;

		private readonly FileSystemWatcher _watcher;
		private readonly INativeMethods _nativeMethods;
		private readonly IWrapperService _wrapperService;

		private readonly SignalableAction _action;

		public InstrumentationWatcher(INativeMethods nativeMethods, IWrapperService wrapperService)
		{
			_nativeMethods = nativeMethods;
			_wrapperService = wrapperService;
			_action = new SignalableAction(RequestRejit, RequestRejitDelayMilliseconds);
			_watcher = new FileSystemWatcher();
			_subscriptions.Add<ServerConfigurationUpdatedEvent>(OnServerConfigurationUpdated);
		}

		public void Start()
		{
			if (AgentInstallConfiguration.HomeExtensionsDirectory == null)
			{
				Log.WarnFormat("Live instrumentation updates due to instrumentation file changes will not be applied because HomeExtensionsDirectory is null.");
				return;
			}

			_action.Start();

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

		public override void Dispose()
		{
			_action?.Dispose();
			_watcher?.Dispose();

			base.Dispose();
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			// This implementation only exists to satisfy the derivation from ConfigurationBasedService, which exists for access to
			// to the liveInstrumentation configuration option, which is influenced by the highSecurity configuration.
		}

		private void OnServerConfigurationUpdated([NotNull] ServerConfigurationUpdatedEvent serverConfigurationUpdatedEvent)
		{
			var instrumentation = serverConfigurationUpdatedEvent.Configuration.Instrumentation;

			if (instrumentation != null && !instrumentation.IsEmpty())
			{
				if (_configuration.LiveInstrumentationEnabled)
				{
					try
					{
						foreach (var instrumentationSet in instrumentation)
						{
							_nativeMethods.AddCustomInstrumentation(instrumentationSet.Name, instrumentationSet.Config);
						}

						Log.InfoFormat("Applying live instrumentation");

						// We want to apply custom instrumentation regardless of whether or not any was received on
						// this connect because we may have received instrumentation on a previous connect.
						_nativeMethods.ApplyCustomInstrumentation();
					}
					catch (Exception ex)
					{
						Log.Error(ex);
					}
				}
				else
				{
					Log.WarnFormat("Live instrumentation received from server not applied due to configuration.");
				}
			}
		}
	}
}
