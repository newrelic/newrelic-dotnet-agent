using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	public class InstrumentationSet
	{
		public InstrumentationSet(string name)
		{
			Name = name;
		}

		public string Name { get; }
		public List<InstrumentationPoint> InstrumentationPoints { get; } = new List<InstrumentationPoint>();

		public void Add(params InstrumentationPoint[] instrumentationPoints)
		{
			InstrumentationPoints.AddRange(instrumentationPoints);
		}
	}
}
