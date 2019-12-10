namespace NewRelic.OpenTracing.AmazonLambda.Util
{
	public class PayloadExtractAdapter : IPayload
	{
		private string _payload;

		public PayloadExtractAdapter(string payload)
		{
			_payload = payload;
		}

		public string GetPayload => _payload;
	}
}
