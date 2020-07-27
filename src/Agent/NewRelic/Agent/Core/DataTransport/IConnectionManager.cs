using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IConnectionManager
    {
        T SendDataRequest<T>([NotNull] String method, [NotNull] params Object[] data);
    }
}
