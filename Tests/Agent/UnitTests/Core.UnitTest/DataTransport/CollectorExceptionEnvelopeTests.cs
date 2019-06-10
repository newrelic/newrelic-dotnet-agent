using System;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DataTransport
{
	[TestFixture]
	public class CollectorExceptionEnvelopeTests
	{
		[Test]
		[TestCase("NewRelic::Agent::ForceDisconnectException", typeof (ForceDisconnectException))]
		[TestCase("NewRelic::Agent::ForceRestartException", typeof (ForceRestartException))]
		[TestCase("NewRelic::Agent::PostTooBigException", typeof (PostTooBigException))]
		[TestCase("NewRelic::Agent::RuntimeError", typeof (RuntimeException))]
		[TestCase("NewRelic::Agent::LicenseException", typeof (LicenseException))]
		[TestCase("ForceDisconnectException", typeof (ForceDisconnectException))]
		[TestCase("ForceRestartException", typeof (ForceRestartException))]
		[TestCase("PostTooBigException", typeof(PostTooBigException))]
		[TestCase("RuntimeError", typeof(RuntimeException))]
		[TestCase("unknown", typeof(ExceptionFactories.UnknownRPMException))]
		public void deserializes_from_object_exception(string errorType, Type expectedExceptionType)
		{
			var json = string.Format(@"{{""error_type"": ""{0}"", ""message"": ""foo""}}", errorType);

			var result = JsonConvert.DeserializeObject<CollectorExceptionEnvelope>(json);

			Assert.NotNull(result);
			Assert.NotNull(result.Exception);
			NrAssert.Multiple
				(
					() => Assert.IsInstanceOf(expectedExceptionType, result.Exception),
					() => Assert.AreEqual("foo", result.Exception.Message)
				);
		}

		[Test]
		public void deserializes_from_string_exception()
		{
			const string json = @"""banana""";

			var result = JsonConvert.DeserializeObject<CollectorExceptionEnvelope>(json);

			Assert.NotNull(result);
			Assert.NotNull(result.Exception);
			NrAssert.Multiple
				(
					() => Assert.IsInstanceOf<Exception>(result.Exception),
					() => Assert.AreEqual("banana", result.Exception.Message)
				);
			Assert.IsInstanceOf<Exception>(result.Exception);
		}
	}
}
