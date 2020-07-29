/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class ExplainPlan
    {
        public List<string> ExplainPlanHeaders;
        public List<List<object>> ExplainPlanDatas;
        public List<int> ObfuscatedHeaders;

        public ExplainPlan(List<string> explainPlanHeaders, List<List<object>> explainPlanDatas, List<int> obfuscatedHeaders)
        {
            if (explainPlanHeaders == null)
                throw new ArgumentException();
            if (explainPlanDatas == null)
                throw new ArgumentException();
            if (obfuscatedHeaders == null)
                throw new ArgumentException();

            ExplainPlanHeaders = explainPlanHeaders;
            ExplainPlanDatas = explainPlanDatas;
            ObfuscatedHeaders = obfuscatedHeaders;
        }
    }
}
