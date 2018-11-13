namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	public interface IDistributedTraceApiModel
	{
		string HttpSafe();

		string Text();

		bool IsEmpty();
	}
}