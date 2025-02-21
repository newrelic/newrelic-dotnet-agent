// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Providers.Wrapper.AzureFunction;

public class InProcessFunctionDetails
{
    public string TriggerType { get; set; }
    public bool IsWebTrigger => TriggerType == "http";
    public string FunctionName { get; set; }
}
