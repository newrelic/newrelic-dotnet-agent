using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;

namespace NewRelic.Agent.Core.Segments
{
	public class NoOpSegment : ISegment, ISegmentExperimental
	{
		private static readonly ISegmentData _noOpSegmentData = new SimpleSegmentData("NoOpSegment");

		public bool IsValid => false;
		public bool DurationShouldBeDeductedFromParent { get; set; } = false;
		public bool IsLeaf => false;
		public bool IsExternal => false;
		public string SpanId => null;

		public ISegmentData SegmentData => _noOpSegmentData;

		public void End() { }
		public void End(Exception ex) { }
		public void MakeCombinable() { }

		public ISegmentExperimental MakeLeaf()
		{
			return this;
		}

		public void RemoveSegmentFromCallStack() { }

		public ISegmentExperimental SetSegmentData(ISegmentData segmentData)
		{
			return this;
		}

		public ISpan AddCustomAttribute(string key, object value)
		{
			return this;
		}
	}
}
