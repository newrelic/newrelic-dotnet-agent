/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class JavaScriptAgent
    {
        public static Dictionary<string, string> GetJavaScriptAgentConfigFromSource(string source)
        {
            const string regex = @"<script type=""text/javascript"">window.NREUM\|\|\(NREUM={}\);NREUM.info = (.*?)</script>";
            var match = Regex.Match(source, regex);
            Assert.True(match.Success, "Did not find a match for the JavaScript agent config in the provided page.");
            var json = match.Groups[1].Value;
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
    }
}
