// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace NewRelic.Agent.Extensions.AwsSdk;

public static class AwsRegionValidator
{
    // e.g. us-east-2, eu-west-1, us-gov-west-1, us-iso-east-1
    private static readonly Regex RegionRegex = new Regex(@"^[a-z]{2}((-gov)|(-iso([a-z]?)))?-[a-z]+-\d{1}$", RegexOptions.Compiled);

    public static bool LooksLikeARegion(string text) => !string.IsNullOrEmpty(text) && RegionRegex.IsMatch(text);
}
