using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Samplers
{
	public class GCSamplerNetCore : AbstractSampler
	{

		public struct SamplerIsApplicableToFrameworkResult
		{
			public bool Result { get; set; }

			public SamplerIsApplicableToFrameworkResult(bool value)
			{
				Result = value;
			}
		}

		private IGCEventsListener _eventsListener;
		private readonly Func<IGCEventsListener> _eventListenerFactory;
		private readonly IGcSampleTransformer _transformer;

		protected override bool Enabled => base.Enabled 
			&& (_configuration?.EventListenerSamplersEnabled).GetValueOrDefault(false) 
			&& _fxSamplerIsApplicableToFramework().Result;

		/// <summary>
		/// Method determines if the event source that we are interested in will be available on this framework (net-core vs. net-framework).
		/// If the value is, false, this sampler should be disabled
		/// </summary>
		/// <returns></returns>
		private Func<SamplerIsApplicableToFrameworkResult> _fxSamplerIsApplicableToFramework;


		/// <summary>
		/// Backing property for similarly named function.
		/// </summary>
		private static bool? _fxSamplerIsApplicableToFrameworkDefaultValue = null;

		/// <summary>
		/// This method was taken from .Net Core code.  It validates that the current application is a .Net Core 2.2
		/// or above application.
		/// </summary>
		/// <returns></returns>
		public static SamplerIsApplicableToFrameworkResult FXsamplerIsApplicableToFrameworkDefault()
		{
			if (_fxSamplerIsApplicableToFrameworkDefaultValue.HasValue)
			{
				return new SamplerIsApplicableToFrameworkResult(_fxSamplerIsApplicableToFrameworkDefaultValue.Value);
			}

			//Disable Event Listeners on Linux due to memory leak
			if (!AgentInstallConfiguration.IsWindows)
			{
				_fxSamplerIsApplicableToFrameworkDefaultValue = false;
				return new SamplerIsApplicableToFrameworkResult(_fxSamplerIsApplicableToFrameworkDefaultValue.Value);
			}

			var spc = typeof(EventSource).Assembly;
			if (spc == null)
			{
				_fxSamplerIsApplicableToFrameworkDefaultValue = false;
				return new SamplerIsApplicableToFrameworkResult(_fxSamplerIsApplicableToFrameworkDefaultValue.Value);
			}

			//This is the EventSource that we try to subscribe to
			Type runtimeEventSourceType = spc.GetType("System.Diagnostics.Tracing.RuntimeEventSource");
			if (runtimeEventSourceType == null)
			{
				_fxSamplerIsApplicableToFrameworkDefaultValue = false;
				return new SamplerIsApplicableToFrameworkResult(_fxSamplerIsApplicableToFrameworkDefaultValue.Value);
			}

			_fxSamplerIsApplicableToFrameworkDefaultValue = true;
			return new SamplerIsApplicableToFrameworkResult(_fxSamplerIsApplicableToFrameworkDefaultValue.Value);
		}
		
		public GCSamplerNetCore(IScheduler scheduler, Func<IGCEventsListener> eventListenerFactory, IGcSampleTransformer transformer, Func<SamplerIsApplicableToFrameworkResult> fxSamplerIsApplicableToFramework) : base(scheduler, TimeSpan.FromSeconds(1))
		{
			_eventListenerFactory = eventListenerFactory;
			_transformer = transformer;
			_fxSamplerIsApplicableToFramework = fxSamplerIsApplicableToFramework;
		}

		public override void Sample()
		{
			try
			{
				var sampleValues = _eventsListener.Sample();
				_transformer.Transform(sampleValues);
			}
			catch (Exception ex)
			{
				Log.Error($"Unable to get Garbage Collection event listener sample.  Further .NetCore GC metrics will not be collected.  Error : {ex}");
				Stop();
			}
		}

		public override void Start()
		{
			try
			{
				if(!_fxSamplerIsApplicableToFramework().Result)
				{
					Log.Debug($"The GCSamplerNetCore sampler has been disabled because this is not a .Net Core 2.2+ application.");
				}

				base.Start();

				if (!Enabled)
				{
					return;
				}

				_eventsListener = _eventsListener ?? _eventListenerFactory();
			}
			catch(Exception ex)
			{
				Log.Error($"Unable to start Garbage Collection Event Listener Sample.  Further .NetCore GC metrics will not be captured.  Error: {ex}");
				Stop();
			}
		}

		protected override void Stop()
		{
			base.Stop();
			_eventsListener?.Dispose();
			_eventsListener = null;
		}

		public override void Dispose()
		{
			base.Dispose();
			_eventsListener?.Dispose();
			_eventsListener = null;
		}

	}

	public interface IGCEventsListener : IDisposable
	{
		Dictionary<GCSampleType, float> Sample();
	}

	public class GCEventsListener : EventListener, IGCEventsListener
	{
		//The Microsoft EventID to listen to, corresponds to "Microsoft-Windows-DotNETRuntime"
		public static readonly Guid DotNetEventSourceID = Guid.Parse("5e5bb766-bbfc-5662-0548-1d44fad9bb56");

		/// <summary>
		/// Allows us to mainpulate the event source ID for testing purposes
		/// </summary>
		public static Guid EventSourceIDToMonitor = DotNetEventSourceID;

		public const int GCKeyword = 0x1;
		public const int EventID_GCStart = 1;
		public const int EventID_GCHeapStats = 4;

		//These values are stored as objects so as to avoid extra castings
		private object _gen0Size = (ulong)0;
		private object _gen0Promoted = (ulong)0;
		private object _gen1Size = (ulong)0;
		private object _gen1Promoted = (ulong)0;
		private object _gen2Size = (ulong)0;
		private object _gen2Promoted = (ulong)0;
		private object _lohSize = (ulong)0;
		private object _lohPromoted = (ulong)0;
		private object _handlesCount = (uint)0;

		private InterlockedCounter _inducedCount = new InterlockedCounter();

		private readonly InterlockedCounter[] _collectionCountPerGen;

		public GCEventsListener()
		{
			_collectionCountPerGen = new InterlockedCounter[GC.MaxGeneration + 1];
			for (var i = 0; i < _collectionCountPerGen.Length; i++)
			{
				_collectionCountPerGen[i] = new InterlockedCounter();
			}
		}

		protected override void OnEventSourceCreated(EventSource eventSource)
		{
			if (eventSource.Guid == EventSourceIDToMonitor)
			{
				EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)GCKeyword);
				base.OnEventSourceCreated(eventSource);
			}
		}

		protected override void OnEventWritten(EventWrittenEventArgs eventData)
		{
			switch (eventData.EventId)
			{
				case EventID_GCStart:
					ProcessEvent_GCStart(eventData);
					break;

				case EventID_GCHeapStats:
					ProcessEvent_GCHeapStats(eventData);
					break;
			}
		}

		private void ProcessEvent_GCStart(EventWrittenEventArgs eventArgs)
		{
			//Identify if the GC was induced by code
			const uint reasonID_Induced = 0x1;
			const uint reasonID_InducedNotBlocking = 0x7;

			var reason = (uint)eventArgs.Payload[2];
			if(reason == reasonID_Induced || reason == reasonID_InducedNotBlocking)
			{
				_inducedCount.Increment();
			}

			//Update Collection Counters for each generation including and below the GC depth requested
			var depth = (uint)eventArgs.Payload[1];
			for(var i = 0; i <= depth; i++)
			{
				_collectionCountPerGen[i].Increment();
			}
			
		}

		private void ProcessEvent_GCHeapStats(EventWrittenEventArgs eventArgs)
		{
			Interlocked.Exchange(ref _gen0Size, eventArgs.Payload[0]);
			Interlocked.Exchange(ref _gen0Promoted, eventArgs.Payload[1]);
			Interlocked.Exchange(ref _gen1Size, eventArgs.Payload[2]);
			Interlocked.Exchange(ref _gen1Promoted, eventArgs.Payload[3]);
			Interlocked.Exchange(ref _gen2Size, eventArgs.Payload[4]);
			Interlocked.Exchange(ref _gen2Promoted, eventArgs.Payload[5]);
			Interlocked.Exchange(ref _lohSize, eventArgs.Payload[6]);
			Interlocked.Exchange(ref _lohPromoted, eventArgs.Payload[7]);
			Interlocked.Exchange(ref _handlesCount, eventArgs.Payload[12]);
		}

		public Dictionary<GCSampleType, float> Sample()
		{
			var inducedCount = _inducedCount.Exchange(0);
			
			var result = new Dictionary<GCSampleType, float>()
			{
				{ GCSampleType.Gen0Size, (ulong)_gen0Size },
				{ GCSampleType.Gen0Promoted, (ulong)_gen0Promoted },
				{ GCSampleType.Gen1Size, (ulong)_gen1Size },
				{ GCSampleType.Gen1Promoted, (ulong)_gen1Promoted },
				{ GCSampleType.Gen2Size, (ulong)_gen2Size },
				{ GCSampleType.Gen2Survived, (ulong)_gen2Promoted },
				{ GCSampleType.LOHSize, (ulong)_lohSize },
				{ GCSampleType.LOHSurvived, (ulong)_lohPromoted },
				{ GCSampleType.HandlesCount, (uint)_handlesCount },
				{ GCSampleType.InducedCount, inducedCount }
			};

			if (_collectionCountPerGen.Length > 0) result[GCSampleType.Gen0CollectionCount] = _collectionCountPerGen[0].Exchange(0);
			if (_collectionCountPerGen.Length > 1) result[GCSampleType.Gen1CollectionCount] = _collectionCountPerGen[1].Exchange(0);
			if (_collectionCountPerGen.Length > 2) result[GCSampleType.Gen2CollectionCount] = _collectionCountPerGen[2].Exchange(0);

			return result;
		}
	}



}

