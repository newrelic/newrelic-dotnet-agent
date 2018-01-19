using System;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Utilization
{
	public interface IVendorModel
	{
		[JsonIgnore]
		String VendorName { get; }
	}
}
