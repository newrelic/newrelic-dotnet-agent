using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NewRelic.Agent.Core.Aggregators
{
    /// <summary>
    /// Used to pass metric data to the Metric Aggregator.
    /// </summary>
    public interface IAllMetricStatsCollection
    {

        void AddMetricsToEngine(MetricStatsCollection engine);
    }
}
