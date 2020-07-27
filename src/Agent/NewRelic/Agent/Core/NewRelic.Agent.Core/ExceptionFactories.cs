using System;
using System.Collections.Generic;
using System.Net;
using NewRelic.Agent.Core.Exceptions;

namespace NewRelic.Agent.Core
{

    public class ExceptionFactories
    {
        private readonly static IDictionary<string, IExceptionFactory> rubyClassToType;
        private readonly static IDictionary<HttpStatusCode, IExceptionFactory> statusCodeToType;

        static ExceptionFactories()
        {
            rubyClassToType = new Dictionary<string, IExceptionFactory>();
            statusCodeToType = new Dictionary<HttpStatusCode, IExceptionFactory>();

            rubyClassToType.Add("NewRelic::Agent::ForceDisconnectException",
                                new ForceDisconnectExceptionFactory());
            rubyClassToType.Add("NewRelic::Agent::ForceRestartException",
                                new ForceRestartExceptionFactory());
            rubyClassToType.Add("NewRelic::Agent::PostTooBigException",
                                new PostTooBigExceptionFactory());
            rubyClassToType.Add("NewRelic::Agent::RuntimeError",
                                new RuntimeExceptionFactory());

            rubyClassToType.Add("NewRelic::Agent::LicenseException",
                                new LicenseExceptionFactory());

            rubyClassToType.Add("ForceDisconnectException",
                                new ForceDisconnectExceptionFactory());
            rubyClassToType.Add("ForceRestartException",
                                new ForceRestartExceptionFactory());
            rubyClassToType.Add("PostTooBigException",
                                new PostTooBigExceptionFactory());
            rubyClassToType.Add("RuntimeError",
                                new RuntimeExceptionFactory());

            statusCodeToType.Add(HttpStatusCode.UnsupportedMediaType, new SerializationExceptionFactory());
            statusCodeToType.Add(HttpStatusCode.RequestEntityTooLarge, new PostTooLargeExceptionFactory());
            statusCodeToType.Add(HttpStatusCode.ServiceUnavailable, new ServiceUnavailableExceptionFactory());
        }

        private ExceptionFactories()
        {
        }
        public static Exception NewException(HttpStatusCode statusCode, string statusDescription)
        {
            IExceptionFactory factory;
            if (statusCodeToType.TryGetValue(statusCode, out factory))
            {
                return factory.CreateException(statusDescription);
            }
            return new HttpException(statusCode, statusDescription);
        }

        /// <summary>
        /// Make a new exception whose name in RPM space is given by type.
        /// </summary>
        /// <param name="type">Type of the exception, as given in some well-known RPM/collector space.</param>
        /// <param name="message">Message payload for the constructed exception.</param>
        /// <returns>A created exception</returns>
        public static Exception NewException(string type, string message)
        {
            IExceptionFactory factory;
            if (rubyClassToType.TryGetValue(type, out factory))
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
            public UnknownRPMException(string message) : base(message)
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

        private class RuntimeExceptionFactory : IExceptionFactory
        {
            public Exception CreateException(string message)
            {
                return new RuntimeException(message);
            }
        }

        private class ServiceUnavailableExceptionFactory : IExceptionFactory
        {
            public Exception CreateException(string message)
            {
                return new ServiceUnavailableException(message);
            }
        }

        private class SerializationExceptionFactory : IExceptionFactory
        {
            public Exception CreateException(string message)
            {
                return new SerializationException(message);
            }
        }

        private class PostTooLargeExceptionFactory : IExceptionFactory
        {
            public Exception CreateException(string message)
            {
                return new PostTooLargeException(message);
            }
        }

        private class LicenseExceptionFactory : IExceptionFactory
        {
            public Exception CreateException(string message)
            {
                return new LicenseException(message);
            }
        }

        private class ForceDisconnectExceptionFactory : IExceptionFactory
        {
            public Exception CreateException(string message)
            {
                return new ForceDisconnectException(message);
            }
        }

        private class ForceRestartExceptionFactory : IExceptionFactory
        {
            public Exception CreateException(string message)
            {
                return new ForceRestartException(message);
            }
        }

        private class PostTooBigExceptionFactory : IExceptionFactory
        {
            public Exception CreateException(string message)
            {
                return new PostTooBigException(message);
            }
        }

        private interface IExceptionFactory
        {
            Exception CreateException(string message);
        }

    }

}
