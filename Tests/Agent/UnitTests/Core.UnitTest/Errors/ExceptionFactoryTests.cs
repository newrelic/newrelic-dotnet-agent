using System;
using System.Net;
using NewRelic.Agent.Core.Exceptions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Errors.UnitTest
{
	[TestFixture]
	public class ExceptionFactoryTests
	{
		[Test]
		public void CreateNewException()
		{
			var serverErrorException = ExceptionFactories.NewException(HttpStatusCode.InternalServerError, "InternalServerError");

			var custom5xxErrorException = ExceptionFactories.NewException((HttpStatusCode)555, "CustomServerError");

			var unsupportedMediaTypeException = ExceptionFactories.NewException(HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType");

			var otherHttpException = ExceptionFactories.NewException((HttpStatusCode)300, "OtherHttpException");

			Assert.IsTrue(serverErrorException is ServerErrorException);

			Assert.IsTrue(custom5xxErrorException is ServerErrorException);

			Assert.IsTrue(otherHttpException is HttpException);

			Assert.IsTrue(unsupportedMediaTypeException is SerializationException);

		}

	}
}