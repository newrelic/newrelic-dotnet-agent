// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Labels;

public class Label
{
    [JsonProperty(PropertyName = "label_type")]
    public readonly string Type;

    [JsonProperty(PropertyName = "label_value")]
    public readonly string Value;

    public Label(string labelType, string labelValue)
    {
        if (labelType == null)
            throw new ArgumentNullException("labelType");
        if (labelValue == null)
            throw new ArgumentNullException("labelValue");

        Type = labelType;
        Value = labelValue;
    }
}