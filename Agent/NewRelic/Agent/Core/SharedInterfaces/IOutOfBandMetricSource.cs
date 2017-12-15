using JetBrains.Annotations;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.SharedInterfaces
{
	public delegate void PublishMetricDelegate([NotNull] MetricWireModel metric);

	public interface IOutOfBandMetricSource
	{
		void RegisterPublishMetricHandler([NotNull] PublishMetricDelegate publishMetricDelegate);
	}
}
