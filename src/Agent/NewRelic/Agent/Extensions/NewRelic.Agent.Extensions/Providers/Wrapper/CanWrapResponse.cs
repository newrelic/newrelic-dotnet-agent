// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class CanWrapResponse
    {
        public bool CanWrap { get; }

        public string AdditionalInformation { get; }

        public bool SuppressDefaultWrapperDebugMessage { get; }

        public CanWrapResponse(bool canWrap, string additionalInformation = null, bool suppressDefaultWrapperDebugMessage = false)
        {
            CanWrap = canWrap;
            AdditionalInformation = additionalInformation;
            SuppressDefaultWrapperDebugMessage = suppressDefaultWrapperDebugMessage;
        }
    }
}
