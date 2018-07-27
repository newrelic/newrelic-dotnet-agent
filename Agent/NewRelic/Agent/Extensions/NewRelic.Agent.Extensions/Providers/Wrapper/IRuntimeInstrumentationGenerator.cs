namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	public interface IRuntimeInstrumentationGenerator
	{
		InstrumentationSet GetInstrumentation();
	}
}