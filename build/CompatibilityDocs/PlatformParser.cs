// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text.RegularExpressions;

namespace CompatibilityDocs;

public static class PlatformParser
{
    public static Platform Parse(string tab)
    {
        return tab?.Trim().ToLowerInvariant() switch
        {
            "core" => Platform.Core,
            "framework" => Platform.Framework,
            _ => throw new ArgumentException($"Unknown platform tab '{tab}'. Expected 'core' or 'framework'.")
        };
    }

    public static Platform? TfmToPlatform(string tfm)
    {
        if (string.IsNullOrWhiteSpace(tfm))
            return null;

        var t = tfm.Trim().ToLowerInvariant();
        if (t.StartsWith("net4"))
            return Platform.Framework;
        if (t.StartsWith("netcoreapp"))
            return Platform.Core;
        // net5.0 .. net99.0 -> Core; net4x already handled above
        if (Regex.IsMatch(t, @"^net\d+\.\d+$"))
            return Platform.Core;
        return null;
    }
}
