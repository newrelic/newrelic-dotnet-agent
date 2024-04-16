// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Llm
{
    public class LlmErrorData
    {
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorParam { get; set; }
        public string HttpStatusCode { get; set; }
    }
}
