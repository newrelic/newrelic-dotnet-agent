using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Utilities
{
    public class RequestSubscription<TRequest, TResponse> : IDisposable
    {
        [NotNull] private readonly RequestBus<TRequest, TResponse>.RequestHandler _requestHandler;

        public RequestSubscription([NotNull] RequestBus<TRequest, TResponse>.RequestHandler requestHandler)
        {
            _requestHandler = requestHandler;
            RequestBus<TRequest, TResponse>.AddResponder(_requestHandler);
        }

        public void Dispose()
        {
            RequestBus<TRequest, TResponse>.RemoveResponder(_requestHandler);
        }
    }
}
