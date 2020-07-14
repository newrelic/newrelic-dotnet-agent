using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
	[JsonConverter(typeof(JsonArrayConverter))]
	public class ExplainPlanWireModel
	{
		private readonly ExplainPlan _explainPlan;

		[JsonArrayIndex(Index = 0)]
		public IEnumerable<String> ExplainPlanHeaders
		{
			get { return _explainPlan.ExplainPlanHeaders; }
		}

		[JsonArrayIndex(Index = 1)]
		public List<List<Object>> ExplainPlanDatas
		{
			get { return _explainPlan.ExplainPlanDatas; }
		}

		public ExplainPlanWireModel(ExplainPlan explainPlan)
		{
			_explainPlan = explainPlan;
		}
	}
}
