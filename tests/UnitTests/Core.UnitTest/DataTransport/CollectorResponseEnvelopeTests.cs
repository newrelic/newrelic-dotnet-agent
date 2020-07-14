using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DataTransport
{
	[TestFixture]
	public class CollectorResponseEnvelopeTests
	{
		[Test]
		public void deserializes_from_successful_response()
		{
			const String json = @"{""return_value"": ""Hello!""}";

			var result = JsonConvert.DeserializeObject<CollectorResponseEnvelope<String>>(json);

			Assert.NotNull(result);
			Assert.AreEqual("Hello!", result.ReturnValue);
		}

		[Test]
		public void deserializes_from_error_response()
		{
			const String json = @"{""exception"": ""banana""}";

			var result = JsonConvert.DeserializeObject<CollectorResponseEnvelope<String>>(json);

			Assert.NotNull(result);
			Assert.NotNull(result.CollectorExceptionEnvelope);
			Assert.AreEqual("banana", result.CollectorExceptionEnvelope.Exception.Message);
		}
	}
}
