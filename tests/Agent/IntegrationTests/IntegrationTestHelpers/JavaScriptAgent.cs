// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers;

public static class JavaScriptAgent
{
    public static Dictionary<string, string> GetJavaScriptAgentConfigFromSource(string source)
    {
        const string regex = @"<script type=""text/javascript"">window.NREUM\|\|\(NREUM={}\);NREUM.info = (.*?)</script>";
        var match = Regex.Match(source, regex, RegexOptions.Singleline);
        Assert.True(match.Success, "Did not find a match for the JavaScript agent config in the provided page.");
        var json = match.Groups[1].Value;
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    public static string GetJavaScriptAgentScriptFromSource(string source)
    {
        const string regex = @"<script type=""text/javascript"">(.*?)</script>";
        var matches = Regex.Matches(source, regex, RegexOptions.Singleline);
        if (matches.Count <= 0)
            return null;

        var match = matches[1]; //specifically look for the 2nd match. The first match contains settings. The 2nd match contains the actual browser agent.
        Assert.True(match.Success, "Did not find a match for the JavaScript agent config in the provided page.");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Returns the offset of every non-overlapping NREUM.info block in the source. Exactly one
    /// offset means the browser agent script was injected exactly once; more than one means the
    /// agent injected it multiple times into a single response.
    /// </summary>
    public static List<int> FindInfoBlockOffsets(string source)
    {
        const string marker = "NREUM.info";

        var offsets = new List<int>();
        var searchStart = 0;
        while (true)
        {
            var index = source.IndexOf(marker, searchStart, StringComparison.Ordinal);
            if (index < 0)
                break;

            offsets.Add(index);
            searchStart = index + marker.Length;
        }

        return offsets;
    }
}