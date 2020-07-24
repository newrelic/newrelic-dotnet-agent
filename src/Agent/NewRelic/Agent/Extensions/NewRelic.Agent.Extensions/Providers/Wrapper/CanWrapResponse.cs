using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class CanWrapResponse
    {
        public Boolean CanWrap;

        [CanBeNull]
        public String AdditionalInformation;

        public CanWrapResponse(Boolean canWrap, [CanBeNull] String additionalInformation = null)
        {
            CanWrap = canWrap;
            AdditionalInformation = additionalInformation;
        }
    }
}
