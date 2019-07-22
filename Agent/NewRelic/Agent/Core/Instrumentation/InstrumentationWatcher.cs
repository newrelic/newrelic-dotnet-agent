using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Core.Logging;
using System;
using System.IO;

namespace NewRelic.Agent.Core.Instrumentation
{
	public class InstrumentationWatcher : IDisposable
	{
		private const int RequestRejitDelayMilliseconds = 15000;

		private readonly FileSystemWatcher _watcher;
		private readonly IInstrumentationService _instrumentationService;
		private readonly IWrapperService _wrapperService;

		private readonly SignalableAction _action;

		public InstrumentationWatcher(IWrapperService wrapperService, IInstrumentationService instrumentationService)
		{
			_wrapperService = wrapperService;
			_instrumentationService = instrumentationService;
			_action = new SignalableAction(RequestRejit, RequestRejitDelayMilliseconds);
			_watcher = new FileSystemWatcher();
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
			Log.Info("Starting instrumentation refresh from InstrumentationWatcher");
			var result = _instrumentationService.InstrumentationRefresh();
			_wrapperService.ClearCaches();
			Log.InfoFormat("Completed instrumentation refresh from InstrumentationWatcher: {0}", result);
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
	}
}
