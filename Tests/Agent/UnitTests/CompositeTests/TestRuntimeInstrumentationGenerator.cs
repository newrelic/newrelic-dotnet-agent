using System.Reflection;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace CompositeTests
{
	public class TestRuntimeInstrumentationGenerator : IRuntimeInstrumentationGenerator
	{
		public InstrumentationSet GetInstrumentation()
		{
			var currentMethod = MethodBase.GetCurrentMethod() as MethodInfo;
			var instrumentationSet = new InstrumentationSet("RuntimeInstrumentation");
			var instrumentationPoint = new InstrumentationPoint(currentMethod);
			instrumentationSet.Add(instrumentationPoint);
			return instrumentationSet;
		}
	}
}