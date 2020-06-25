/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.OpenTracing.AmazonLambda.Util
{
    public class PayloadExtractAdapter : IPayload
    {
        private string _payload;

        public PayloadExtractAdapter(string payload)
        {
            _payload = payload;
        }

        public string GetPayload => _payload;
    }
}
