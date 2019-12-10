namespace NewRelic.OpenTracing.AmazonLambda.Util
{
	public interface IPayload
	{
		string GetPayload { get; }
	}
}
