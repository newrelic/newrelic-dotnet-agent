using System;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
	public class AwsVendorModel : IVendorModel
	{
		private readonly String _availabilityZone;
		private readonly String _instanceId;
		private readonly String _instanceType;

		public AwsVendorModel(String availabilityZone, String instanceId, String instanceType)
		{
			_availabilityZone = availabilityZone;
			_instanceId = instanceId;
			_instanceType = instanceType;
		}

		public String VendorName { get { return "aws"; } }

		[JsonProperty("availabilityZone", NullValueHandling = NullValueHandling.Ignore)]
		public String AvailabilityZone { get { return _availabilityZone; } }

		[JsonProperty("instanceId", NullValueHandling = NullValueHandling.Ignore)]
		public String InstanceId { get { return _instanceId; } }

		[JsonProperty("instanceType", NullValueHandling = NullValueHandling.Ignore)]
		public String InstanceType { get { return _instanceType; } }
	}
}
