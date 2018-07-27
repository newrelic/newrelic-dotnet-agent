using System.Reflection;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	public class InstrumentationPoint
	{
		public InstrumentationPoint(MethodInfo methodInfo)
		{
			MethodInfo = methodInfo;
		}

		public InstrumentationPoint(MethodInfo methodInfo, string tracerFactory)
			: this(methodInfo)
		{
			TracerFactory = tracerFactory;
		}

		public MethodInfo MethodInfo { get; }
		public string TracerFactory { get; }

		public override string ToString()
		{
			return $"[MethodInfo: {MethodInfo} -> TracerFactory: {TracerFactory}]";
		}
	}
}