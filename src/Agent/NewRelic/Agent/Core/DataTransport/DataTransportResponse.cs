using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.DataTransport
{
    public struct DataTransportResponse<T>
    {
        public readonly DataTransportResponseStatus Status;
        [CanBeNull]
        public readonly T ReturnValue;

        public DataTransportResponse(DataTransportResponseStatus status, T returnValue = default(T))
        {
            Status = status;
            ReturnValue = returnValue;
        }
    }
}
