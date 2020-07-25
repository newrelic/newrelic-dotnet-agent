using System;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class CanWrapResponse
    {
        public Boolean CanWrap;
        public String AdditionalInformation;

        public CanWrapResponse(Boolean canWrap, String additionalInformation = null)
        {
            CanWrap = canWrap;
            AdditionalInformation = additionalInformation;
        }
    }
}
