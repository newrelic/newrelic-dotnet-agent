// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels;

[JsonConverter(typeof(JsonArrayConverter))]
public class ExplainPlanWireModel
{
    private readonly ExplainPlan _explainPlan;

    [JsonArrayIndex(Index = 0)]
    public IEnumerable<string> ExplainPlanHeaders
    {
        get { return _explainPlan.ExplainPlanHeaders; }
    }

    [JsonArrayIndex(Index = 1)]
    public List<List<object>> ExplainPlanDatas
    {
        get { return _explainPlan.ExplainPlanDatas; }
    }

    public ExplainPlanWireModel(ExplainPlan explainPlan)
    {
        _explainPlan = explainPlan;
    }
}