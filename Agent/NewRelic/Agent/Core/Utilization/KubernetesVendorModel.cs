using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
	public class KubernetesVendorModel : IVendorModel
	{
		private readonly string _kubernetesServiceHost;

		public KubernetesVendorModel(string kubernetesServiceHost)
		{
			_kubernetesServiceHost = kubernetesServiceHost;
		}

		public string VendorName => "kubernetes";

		[JsonProperty("kubernetes_service_host", NullValueHandling = NullValueHandling.Ignore)]
		public string KubernetesServiceHost => _kubernetesServiceHost;
	}
}
