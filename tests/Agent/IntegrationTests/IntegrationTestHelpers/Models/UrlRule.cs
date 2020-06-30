/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using Newtonsoft.Json;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    public class UrlRule
    {
        [JsonProperty("match_expression")]
        public string MatchExpression { get; set; }

        [JsonProperty("replacement")]
        public string Replacement { get; set; }

        [JsonProperty("ignore")]
        public bool Ignore { get; set; }

        [JsonProperty("eval_order")]
        public int EvalOrder { get; set; }

        [JsonProperty("terminate_chain")]
        public bool TerminateChain { get; set; }

        [JsonProperty("replace_all")]
        public bool ReplaceAll { get; set; }

        [JsonProperty("each_segment")]
        public bool EachSegment { get; set; }
    }
}
