// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace W3CTestApp.Models;

public class W3CTestModel
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("arguments")]
    public List<W3CTestModel> Arguments { get; set; }
}
