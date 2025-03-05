// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Providers.Wrapper.AzureFunction;

public class InProcessFunctionDetails
{
    public string Trigger { get; set; }
    public string TriggerTypeName { get; set; }
    public bool IsWebTrigger => Trigger == "http";
    public string FunctionName { get; set; }
}

