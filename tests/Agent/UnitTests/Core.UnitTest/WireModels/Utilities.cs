using NewRelic.Agent.Core.Metrics;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.WireModels
{
    public static class Utilities
    {
        public static IMetricBuilder GetSimpleMetricBuilder()
        {
            var metricNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);
            return new MetricWireModel.MetricBuilder(metricNameService);
        }
    }
}
