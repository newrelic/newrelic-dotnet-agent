using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Instrumentation
{
	public interface IInstrumentationService
	{
		void LoadRuntimeInstrumentation();
		void AddOrUpdateLiveInstrumentation(string name, string xml);
		bool ClearLiveInstrumentation();
		void ApplyInstrumentation();
		int InstrumentationRefresh();
	}

	public class InstrumentationService : IInstrumentationService
	{
		private readonly INativeMethods _nativeMethods;
		private readonly List<IRuntimeInstrumentationGenerator> _runtimeInstrumentationGenerators;
		private readonly IInstrumentationStore _instrumentationStore = new InstrumentationStore();
		private readonly IInstrumentationStore _liveInstrumentationStore = new InstrumentationStore();
		private readonly object _nativeMethodsLock = new object();

		public InstrumentationService(INativeMethods nativeMethods, IEnumerable<IRuntimeInstrumentationGenerator> runtimeInstrumentationGenerators)
		{
			_nativeMethods = nativeMethods;
			_runtimeInstrumentationGenerators = runtimeInstrumentationGenerators.ToList();
		}

		public void LoadRuntimeInstrumentation()
		{
			foreach (var runtimeInstrumentationGenerator in _runtimeInstrumentationGenerators)
			{
				try
				{
					var instrumentationSet = runtimeInstrumentationGenerator.GetInstrumentation();
					_instrumentationStore.AddOrUpdateInstrumentation(instrumentationSet);
				}
				catch (Exception ex)
				{
					Log.Error(ex);
				}
			}
		}

		public void ApplyInstrumentation()
		{
			lock (_nativeMethodsLock)
			{
				if (!_instrumentationStore.IsEmpty || !_liveInstrumentationStore.IsEmpty)
				{
					Log.Info("Applying additional Agent instrumentation");
					foreach (var instrumentationSet in _instrumentationStore.GetInstrumentation())
					{
						_nativeMethods.AddCustomInstrumentation(instrumentationSet.Key, instrumentationSet.Value);
					}
					foreach (var instrumentationSet in _liveInstrumentationStore.GetInstrumentation())
					{
						_nativeMethods.AddCustomInstrumentation(instrumentationSet.Key, instrumentationSet.Value);
					}
					_nativeMethods.ApplyCustomInstrumentation();
				}
				else
				{
					Log.Info("No additional Agent instrumentation to apply.");
				}
			}
		}

		public int InstrumentationRefresh()
		{
			lock (_nativeMethodsLock)
			{
				return _nativeMethods.InstrumentationRefresh();
			}
		}

		public void AddOrUpdateLiveInstrumentation(string name, string xml)
		{
			_liveInstrumentationStore.AddOrUpdateInstrumentation(name, xml);
		}

		public bool ClearLiveInstrumentation()
		{
			return _liveInstrumentationStore.Clear();
		}
	}
}
