using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public class NoOpSegment : ISegment
	{
		public bool IsValid => false;
		public bool DurationShouldBeDeductedFromParent { get; set; } = false;
		public bool IsLeaf => false;
		public bool IsExternal => false;
		public string SpanId => null;

		public void End() { }
		public void End(Exception ex) { }
		public void MakeCombinable() { }
		public void RemoveSegmentFromCallStack() { }
	}
}