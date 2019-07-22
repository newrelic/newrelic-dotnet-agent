using System;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Samplers
{
	public class MemorySampler : AbstractSampler
	{
		private readonly IMemorySampleTransformer _memorySampleTransformer;

		private readonly IProcessStatic _processStatic;

		private const int MemorySampleIntervalSeconds = 1;

		public MemorySampler(IScheduler scheduler, IMemorySampleTransformer memorySampleTransformer, IProcessStatic processStatic)
			: base(scheduler, TimeSpan.FromSeconds(MemorySampleIntervalSeconds))
		{
			_memorySampleTransformer = memorySampleTransformer;
			_processStatic = processStatic;
		}

		public override void Sample()
		{
			try
			{
				var immutableMemorySample = new ImmutableMemorySample(GetCurrentProcessPrivateMemorySize(), GetCurrentProcessVirtualMemorySize(), GetCurrentProcessWorkingSet());
				_memorySampleTransformer.Transform(immutableMemorySample);
			}
			catch (Exception ex)
			{
				Log.Error($"Unable to get Memory sample.  No Memory metrics will be reported.  Error : {ex}");
				Stop();
			}
		}

		private long GetCurrentProcessPrivateMemorySize()
		{
			return _processStatic.GetCurrentProcess().PrivateMemorySize64;
		}
		private long GetCurrentProcessVirtualMemorySize()
		{
			return _processStatic.GetCurrentProcess().VirtualMemorySize64;
		}
		private long GetCurrentProcessWorkingSet()
		{
			return _processStatic.GetCurrentProcess().WorkingSet64;
		}
	}

	public class ImmutableMemorySample
	{
		/// <summary>
		/// Process.PrivateMemorySize64; metric name = Memory/Physical
		/// </summary>
		public readonly long MemoryPrivate;

		/// <summary>
		/// Process.VirtualMemorySize64; metric name = Memory/VirtualMemory 
		/// </summary>
		public readonly long MemoryVirtual;

		/// <summary>
		/// Process.WorkingSet64; metric name = Memory/WorkingSet
		/// </summary>
		public readonly long MemoryWorkingSet;

		public ImmutableMemorySample(long memoryPrivate, long memoryVirtual, long memoryWorkingSet)
		{
			MemoryPrivate = memoryPrivate;
			MemoryVirtual = memoryVirtual;
			MemoryWorkingSet = memoryWorkingSet;
		}
	}
}
