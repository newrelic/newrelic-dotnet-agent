using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class ExplainPlan
    {
        [NotNull]
        public List<String> ExplainPlanHeaders;

        [NotNull]
        public List<List<Object>> ExplainPlanDatas;

        [NotNull]
        public List<Int32> ObfuscatedHeaders;

        public ExplainPlan([NotNull] List<String> explainPlanHeaders, [NotNull] List<List<Object>> explainPlanDatas, [NotNull] List<Int32> obfuscatedHeaders)
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
