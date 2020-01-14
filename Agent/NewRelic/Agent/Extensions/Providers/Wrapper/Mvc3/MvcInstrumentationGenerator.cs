using System.IO;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Mvc3
{
	public class MvcInstrumentationGenerator : IRuntimeInstrumentationGenerator
	{
		public InstrumentationSet GetInstrumentation()
		{
			var instrumentationSet = new InstrumentationSet(nameof(MvcInstrumentationGenerator));
			var instrumentationPoints = GetAsyncActionInstrumentation();
			instrumentationSet.Add(instrumentationPoints);
			return instrumentationSet;
		}

		public InstrumentationPoint[] GetAsyncActionInstrumentation()
		{
			try
			{
				return AsyncActionInstrumentor.GetInstrumentation().ToArray();
			}
			catch(FileNotFoundException)
			{
				//MVC may not be available for non MVC applciations. 
				//Handle this silently.
				return new InstrumentationPoint[0];
			}
		}
	}
}
