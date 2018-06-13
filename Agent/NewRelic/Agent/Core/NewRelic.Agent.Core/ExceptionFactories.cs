using System;
using System.Collections.Generic;
using System.Net;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Exceptions;

namespace NewRelic.Agent.Core
{

	public class ExceptionFactories
	{
		private static readonly IDictionary<String, IExceptionFactory> RubyClassToType;
		private static readonly IDictionary<HttpStatusCode, IExceptionFactory> StatusCodeToType;
		private static readonly ServerErrorExceptionFactory Status5xxErrorExceptionFactory;

		static ExceptionFactories()
		{
			RubyClassToType = new Dictionary<String, IExceptionFactory>();
			StatusCodeToType = new Dictionary<HttpStatusCode, IExceptionFactory>();
			Status5xxErrorExceptionFactory = new ServerErrorExceptionFactory();

			RubyClassToType.Add("NewRelic::Agent::ForceDisconnectException",
			                    new ForceDisconnectExceptionFactory());
			RubyClassToType.Add("NewRelic::Agent::ForceRestartException",
			                    new ForceRestartExceptionFactory());	
			RubyClassToType.Add("NewRelic::Agent::PostTooBigException",
			                    new PostTooBigExceptionFactory());
			RubyClassToType.Add("NewRelic::Agent::RuntimeError",
								new RuntimeExceptionFactory());

		    RubyClassToType.Add("NewRelic::Agent::LicenseException",
		                        new LicenseExceptionFactory());

			RubyClassToType.Add("ForceDisconnectException",
								new ForceDisconnectExceptionFactory());
			RubyClassToType.Add("ForceRestartException",
								new ForceRestartExceptionFactory());
			RubyClassToType.Add("PostTooBigException",
								new PostTooBigExceptionFactory());
			RubyClassToType.Add("RuntimeError",
								new RuntimeExceptionFactory());

			StatusCodeToType.Add(HttpStatusCode.RequestTimeout, new RequestTimeoutExceptionFactory());
			StatusCodeToType.Add(HttpStatusCode.UnsupportedMediaType, new SerializationExceptionFactory());
			StatusCodeToType.Add(HttpStatusCode.RequestEntityTooLarge, new PostTooLargeExceptionFactory());
		}

		private ExceptionFactories ()
		{
		}

		[NotNull]
		public static Exception NewException(HttpStatusCode statusCode, String statusDescription)
		{
			IExceptionFactory factory;
			if (StatusCodeToType.TryGetValue(statusCode, out factory))
			{
				return factory.CreateException(statusDescription);
			}

			if (statusCode >= (HttpStatusCode)500 && statusCode < (HttpStatusCode)600)
			{
				return Status5xxErrorExceptionFactory.CreateException(statusDescription, statusCode);
			}

			return new HttpException(statusCode, statusDescription);
		}
		
		/// <summary>
		/// Make a new exception whose name in RPM space is given by type.
		/// </summary>
		/// <param name="type">Type of the exception, as given in some well-known RPM/collector space.</param>
		/// <param name="message">Message payload for the constructed exception.</param>
		/// <returns>A created exception</returns>
		[NotNull]
		public static Exception NewException(String type, String message) {
			IExceptionFactory factory;
			if (RubyClassToType.TryGetValue(type, out factory))
			{
				return factory.CreateException(message);
			}
			return new UnknownRPMException(message);
		}

		/// <summary>
		/// Thrown when we're given an exception name in RPM/collector space that we don't know about.
		/// </summary>
		public class UnknownRPMException : Exception
		{
			public UnknownRPMException(String message) : base(message)
			{
			}
		}

		private class ConnectionExceptionFactory : IExceptionFactory
		{
			public Exception CreateException(string message)
			{
				return new ConnectionException(message);
			}
		}

		private class RuntimeExceptionFactory : IExceptionFactory {
			public Exception CreateException(String message) {
				return new RuntimeException(message);
			}
		}

		private class RequestTimeoutExceptionFactory : IExceptionFactory
		{
			public Exception CreateException(string message)
			{
				return new RequestTimeoutException(message);
			}
		}

		private class ServerErrorExceptionFactory
		{
			public Exception CreateException(string message, HttpStatusCode statusCode)
			{
				return new ServerErrorException(message, statusCode);
			}
		}

		private class SerializationExceptionFactory : IExceptionFactory {
			public Exception CreateException(string message) {
				return new SerializationException(message);
			}
		}

		private class PostTooLargeExceptionFactory : IExceptionFactory {
			public Exception CreateException(string message) {
				return new PostTooLargeException(message);
			}
		}

		private class LicenseExceptionFactory : IExceptionFactory {
			public Exception CreateException(String message) {
				return new LicenseException(message);
			}
		}
		
		private class ForceDisconnectExceptionFactory : IExceptionFactory {
			public Exception CreateException(String message) {
				return new ForceDisconnectException(message);
			}
		}
		
		private class ForceRestartExceptionFactory : IExceptionFactory {
			public Exception CreateException(String message) {
				return new ForceRestartException(message);
			}
		}
		
		private class PostTooBigExceptionFactory : IExceptionFactory {
			public Exception CreateException(String message) {
				return new PostTooBigException(message);
			}
		}

		private interface IExceptionFactory {
			[NotNull]
			Exception CreateException(String message);
		}

	}
			
}
