using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class ExplainPlan
    {
        public List<String> ExplainPlanHeaders;
        public List<List<Object>> ExplainPlanDatas;
        public List<Int32> ObfuscatedHeaders;

        public ExplainPlan(List<String> explainPlanHeaders, List<List<Object>> explainPlanDatas, List<Int32> obfuscatedHeaders)
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
