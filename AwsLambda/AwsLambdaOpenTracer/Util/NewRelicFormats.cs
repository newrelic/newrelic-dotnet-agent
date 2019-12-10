using OpenTracing.Propagation;

namespace NewRelic.OpenTracing.AmazonLambda.Util
{
	public static class NewRelicFormats
	{
		public static readonly IFormat<IPayload> Payload = new PayloadFormat("PAYLOAD");

		private struct PayloadFormat : IFormat<IPayload>
		{
			private readonly string _name;

			public PayloadFormat(string name)
			{
				_name = name;
			}

			public override string ToString()
			{
				return $"{GetType().Name}.{_name}";
			}
		}
	}
}
