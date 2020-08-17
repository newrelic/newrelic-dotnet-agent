// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class CanWrapResponse
    {
        public bool CanWrap;

        public string AdditionalInformation;

        public CanWrapResponse(bool canWrap, string additionalInformation = null)
        {
            CanWrap = canWrap;
            AdditionalInformation = additionalInformation;
        }
    }
}
