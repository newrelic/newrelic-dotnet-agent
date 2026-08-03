// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace Dotty;

public class ProjectInfo
{
    [JsonPropertyName("projectFile")]
    public string ProjectFile { get; set; }
}