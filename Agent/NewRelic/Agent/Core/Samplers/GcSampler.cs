using System;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Samplers
{
	public enum GCSampleType
	{
		Gen0Size,
		Gen0Promoted,
		Gen1Size,
		Gen1Promoted,
		Gen2Size,
		Gen2Survived,
		LOHSize,
		LOHSurvived,
		HandlesCount,
		InducedCount,
		PercentTimeInGc,
		Gen0CollectionCount,
		Gen1CollectionCount,
		Gen2CollectionCount
	}

#if NETFRAMEWORK

	public class GcSampler : AbstractSampler
	{
		private const string GCPerfCounterCategoryName = ".NET CLR Memory";

		private readonly IGcSampleTransformer _transformer;
		private const int GcSampleInterval = 1;
		private IPerformanceCounterProxyFactory _pcProxyFactory;

		/// <summary>
		/// Translates the sample type enum to the name of the windows performance counter
		/// </summary>
		private readonly Dictionary<GCSampleType, string> _perfCounterNames = new Dictionary<GCSampleType, string>
		{
			{ GCSampleType.Gen0Size , "Gen 0 heap size"},					// BuildByteData (MB)
			{ GCSampleType.Gen0Promoted , "Promoted Memory from Gen 0"},	// BuildByteData
			{ GCSampleType.Gen1Size , "Gen 1 heap size"},					// BuildByteData
			{ GCSampleType.Gen1Promoted , "Promoted Memory from Gen 1"},	// BuildByteData
			{ GCSampleType.Gen2Size , "Gen 2 heap size"},					// BuildByteData
			{ GCSampleType.LOHSize , "Large Object Heap size"},				// BuildByteData
			{ GCSampleType.HandlesCount , "# GC Handles"},					// BuildGaugeValue
			{ GCSampleType.InducedCount , "# Induced GC"},					// BuildGaugeValue ?
			{ GCSampleType.PercentTimeInGc , "% Time in GC"},				// BuildPercentageData
			{ GCSampleType.Gen0CollectionCount , "# Gen 0 Collections"},	// reset on sample}, BuildCountData
			{ GCSampleType.Gen1CollectionCount , "# Gen 1 Collections"},	// reset on sample}, BuildCountData
			{ GCSampleType.Gen2CollectionCount , "# Gen 2 Collections"}		// reset on sample}, BuildCountData
		};

		private readonly Dictionary<GCSampleType, Func<IPerformanceCounterProxy, float>> _perfCounterValueHandlers;

		private float GetPerfCounterValue_Raw(IPerformanceCounterProxy proxy)
		{
			return proxy.NextValue();
		}

		private float GetPerfCounterValue_DeltaValue(IPerformanceCounterProxy proxy, ref float prevValue)
		{
			var currentPerfCounterVal = proxy.NextValue();
			var prevPerfCounterVal = Interlocked.Exchange(ref prevValue, currentPerfCounterVal);
			return currentPerfCounterVal - prevPerfCounterVal;
		}

		private float GetPerfCounterValue_Gen0CollectionCount(IPerformanceCounterProxy proxy)
		{
			return GetPerfCounterValue_DeltaValue(proxy, ref _PrevGen0CollectionCount);
		}

		private float GetPerfCounterValue_Gen1CollectionCount(IPerformanceCounterProxy proxy)
		{
			return GetPerfCounterValue_DeltaValue(proxy, ref _PrevGen1CollectionCount);
		}

		private float GetPerfCounterValue_Gen2CollectionCount(IPerformanceCounterProxy proxy)
		{
			return GetPerfCounterValue_DeltaValue(proxy, ref _PrevGen2CollectionCount);
		}

		private float GetPerfCounterValue_InducedCount(IPerformanceCounterProxy proxy)
		{
			return GetPerfCounterValue_DeltaValue(proxy, ref _PrevInducedCount);
		}

		/// <summary>
		/// Holds the performance counter proxies used to capture the data from the windows performance counters.
		/// </summary>
		private Dictionary<GCSampleType, IPerformanceCounterProxy> _perfCounterProxies;

		// These hold previous values to compute delta between samplings.
		private float _PrevGen0CollectionCount = 0;
		private float _PrevGen1CollectionCount = 0;
		private float _PrevGen2CollectionCount = 0;
		private float _PrevInducedCount = 0;

		public GcSampler(IScheduler scheduler, IGcSampleTransformer gcSampleTransformer, IPerformanceCounterProxyFactory pcProxyFactory)
		 : base(scheduler, TimeSpan.FromSeconds(GcSampleInterval))
		{
			_pcProxyFactory = pcProxyFactory;
			_transformer = gcSampleTransformer;

			//Assign functions to handle the values for the different performance counters collected.
			_perfCounterValueHandlers = new Dictionary<GCSampleType, Func<IPerformanceCounterProxy, float>>()
			{
				{ GCSampleType.Gen0Size, GetPerfCounterValue_Raw },
				{ GCSampleType.Gen0Promoted , GetPerfCounterValue_Raw },
				{ GCSampleType.Gen1Size, GetPerfCounterValue_Raw },
				{ GCSampleType.Gen1Promoted, GetPerfCounterValue_Raw },
				{ GCSampleType.Gen2Size, GetPerfCounterValue_Raw },
				{ GCSampleType.LOHSize, GetPerfCounterValue_Raw },
				{ GCSampleType.HandlesCount, GetPerfCounterValue_Raw },
				{ GCSampleType.InducedCount, GetPerfCounterValue_InducedCount },
				{ GCSampleType.PercentTimeInGc, GetPerfCounterValue_Raw },
				{ GCSampleType.Gen0CollectionCount, GetPerfCounterValue_Gen0CollectionCount },
				{ GCSampleType.Gen1CollectionCount, GetPerfCounterValue_Gen1CollectionCount },
				{ GCSampleType.Gen2CollectionCount, GetPerfCounterValue_Gen2CollectionCount }
			};
		}

		public override void Start()
		{
			if (!Enabled)
			{
				//We always want to do this because we don't want to code with the internals 
				//of the base class in mind.
				base.Start();
				return;
			}

			//Build a set of proxies for each performance counter being sampled.
			_perfCounterProxies = new Dictionary<GCSampleType, IPerformanceCounterProxy>();

			foreach (var sampleTypeEnum in _perfCounterNames.Keys)
			{
				try
				{
					_perfCounterProxies[sampleTypeEnum] = _pcProxyFactory.CreatePerformanceCounterProxy(GCPerfCounterCategoryName, _perfCounterNames[sampleTypeEnum]);
				}
				catch (Exception ex)
				{
					Log.Warn($"Error encountered during the creation of performance counter proxy for GC sample type '{sampleTypeEnum}'.  Metrics of this type will not be captured.  Error : {ex}");
				}
			}

			//We always want to do this because we don't want to code with the internals 
			//of the base class in mind.
			base.Start();

			//If unable to create any proxies, there is no need to run the timer
			if (_perfCounterProxies.Count == 0)
			{
				Log.Warn("No GC performance counters were instantiated.  GC Metrics will not be captured.");
				Stop();
			}
		}

		protected override void Stop()
		{
			base.Stop();

			if (_perfCounterProxies != null)
			{
				foreach (var perfCounterProxy in _perfCounterProxies.Values)
				{
					perfCounterProxy.Dispose();
				}
				_perfCounterProxies = null;
			}
		}

		public override void Sample()
		{
			try
			{
				var sampleValues = new Dictionary<GCSampleType, float>();

				foreach (var proxy in _perfCounterProxies)
				{
					var val = _perfCounterValueHandlers[proxy.Key](proxy.Value);
					sampleValues[proxy.Key] = val;
				}

				_transformer.Transform(sampleValues);
			}
			catch (Exception ex)
			{
				Log.Error($"Unable to get Garbage Collection performance counter sample.  Further .Net GC metrics will not be collected.  Error : {ex}");
				Stop();
			}
		}
	}
#endif
}
