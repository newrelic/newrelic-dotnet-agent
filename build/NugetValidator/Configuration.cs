// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NugetValidator
{
    public class Configuration
    {
        [YamlDotNet.Serialization.YamlMember(Alias = "nuget-packages")]
        public List<string> Packages { get; set; }
    }
}
