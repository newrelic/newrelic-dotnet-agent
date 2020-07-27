using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface ICollectorWire
    {
        [NotNull]
        String SendData([NotNull] String method, [NotNull] ConnectionInfo connectionInfo, [NotNull] String serializedData);
    }
}
