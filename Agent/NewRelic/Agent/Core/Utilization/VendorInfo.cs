using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

#if NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif

namespace NewRelic.Agent.Core.Utilization
{
	public class VendorInfo
	{
		private const int WebReqeustTimeout = 1000;

		private const string ValidateMetadataRegex = @"^[a-zA-Z0-9-_. /]*$";
		private const string ContainerIdRegex = @"[0-9a-f]{64}";

		private const string AwsName = @"aws";
		private const string AzureName = @"azure";
		private const string GcpName = @"gcp";
		private const string PcfName = @"pcf";
		private const string DockerName = @"docker";

		private readonly string AwsUri = @"http://169.254.169.254/2016-09-02/dynamic/instance-identity/document";

		private readonly string AzureUri = @"http://169.254.169.254/metadata/instance/compute?api-version=2017-03-01";
		private const string AzureHeader = @"Metadata: true"; 

		private readonly string GcpUri = @"http://metadata.google.internal/computeMetadata/v1/instance/?recursive=true";
		private const string GcpHeader = @"Metadata-Flavor: Google";  

		private const string PcfInstanceGuid = @"CF_INSTANCE_GUID";
		private const string PcfInstanceIp = @"CF_INSTANCE_IP";
		private const string PcfMemoryLimit = @"MEMORY_LIMIT";

		[NotNull]
		private readonly IConfiguration _configuration;
		[NotNull]
		private readonly IAgentHealthReporter _agentHealthReporter;
		[NotNull]
		private readonly ISystemInfo _systemInfo;

		public VendorInfo([NotNull]IConfiguration configuration, [NotNull] ISystemInfo systemInfo, [NotNull]IAgentHealthReporter agentHealthReporter)
		{
			_configuration = configuration;
			_agentHealthReporter = agentHealthReporter;
			_systemInfo = systemInfo;
		}

		[NotNull]
		public IDictionary<string, IVendorModel> GetVendors()
		{

			var vendors = new Dictionary<string, IVendorModel>();

			var vendorMethods = new List<Func<IVendorModel>>() {};

			if (_configuration.UtilizationDetectAws == true)
				vendorMethods.Add(GetAwsVendorInfo);
			if (_configuration.UtilizationDetectAzure == true)
				vendorMethods.Add(GetAzureVendorInfo);
			if (_configuration.UtilizationDetectGcp == true)
				vendorMethods.Add(GetGcpVendorInfo);
			if (_configuration.UtilizationDetectPcf == true)
				vendorMethods.Add(GetPcfVendorInfo);

			foreach (var vendorMethod in vendorMethods)
			{
				var vendorResult = vendorMethod();

				if (vendorResult != null)
				{
					// We can break out once we've found a vendor as they are mutually exclusive.
					vendors.Add(vendorResult.VendorName, vendorResult);
					break;
				}
			}

			// If Docker info is set to be checked, it must be checked for all vendors.
			if (_configuration.UtilizationDetectDocker == true)
			{
				var dockerVendorInfo = GetDockerVendorInfo();
				if (dockerVendorInfo != null)
					vendors.Add(dockerVendorInfo.VendorName, dockerVendorInfo);
			}

			return vendors;
		}

		[CanBeNull]
		private IVendorModel GetAwsVendorInfo()
		{
			var responseString = GetHttpResponseString(new Uri(AwsUri));
			if (responseString != null)
			{
				return ParseAwsVendorInfo(responseString);
			}

			return null;
		}

		[CanBeNull]
		public IVendorModel ParseAwsVendorInfo([NotNull]string json)
		{
			try
			{
				var jObject = JObject.Parse(json);

				var availabilityZoneToken = jObject.SelectToken("availabilityZone");
				var instanceIdToken = jObject.SelectToken("instanceId");
				var instanceTypeToken = jObject.SelectToken("instanceType");

				var availabilityZone = NormalizeAndValidateMetadata((string)availabilityZoneToken, "availabilityZone", AwsName);
				var instanceId = NormalizeAndValidateMetadata((string)instanceIdToken, "instanceId", AwsName);
				var instanceType = NormalizeAndValidateMetadata((string)instanceTypeToken, "instanceType", AwsName);

				if (availabilityZone == null && instanceId == null && instanceType == null)
				{
					return null;
				}

				return new AwsVendorModel(availabilityZone, instanceId, instanceType);
			}
			catch
			{
				return null;
			}
		}

		[CanBeNull]
		private IVendorModel GetAzureVendorInfo()
		{
			var responseString = GetHttpResponseString(new Uri(AzureUri), new List<string>() { AzureHeader });
			if (responseString != null)
			{
				return ParseAzureVendorInfo(responseString);
			}

			return null;
		}

		[CanBeNull]
		public IVendorModel ParseAzureVendorInfo([NotNull]string json)
		{
			try
			{
				var jObject = JObject.Parse(json);

				var locationToken = jObject.SelectToken("location");
				var nameToken = jObject.SelectToken("name");
				var vmIdToken = jObject.SelectToken("vmId");
				var vmSizeToken = jObject.SelectToken("vmSize");

				var location = NormalizeAndValidateMetadata((string)locationToken, "location", AzureName);
				var name = NormalizeAndValidateMetadata((string)nameToken, "name", AzureName);
				var vmId = NormalizeAndValidateMetadata((string)vmIdToken, "vmId", AzureName);
				var vmSize = NormalizeAndValidateMetadata((string)vmSizeToken, "vmSize", AzureName);

				if ( location == null && name == null && vmId == null && vmSize == null )
				{
					return null;
				}

				return new AzureVendorModel(location, name, vmId, vmSize);
			}
			catch
			{
				return null;
			}
		}

		[CanBeNull]
		private IVendorModel GetGcpVendorInfo()
		{
			var responseString = GetHttpResponseString(new Uri(GcpUri), new List<string>() { GcpHeader });
			if ( responseString != null)
			{
				return ParseGcpVendorInfo(responseString);
			}

			return null;
		}

		[CanBeNull]
		public IVendorModel ParseGcpVendorInfo([NotNull]string json)
		{
			try
			{
				var jObject = JObject.Parse(json);

				var idToken = jObject.SelectToken("id");
				var machineTypeToken = jObject.SelectToken("machineType");
				var nameToken = jObject.SelectToken("name");
				var zoneToken = jObject.SelectToken("zone");

				var id = NormalizeAndValidateMetadata((string)idToken, "id", GcpName);

				var machineTypeString = (string)machineTypeToken;
				var machineType = (machineTypeString != null) ? NormalizeAndValidateMetadata(machineTypeString.Split('/').Last(), "machineType", GcpName) : null;

				var name = NormalizeAndValidateMetadata((string)nameToken, "name", GcpName);

				var zoneTokenString = (string)zoneToken;
				var zone = (zoneTokenString != null) ? NormalizeAndValidateMetadata(zoneTokenString.Split('/').Last(), "zone", GcpName) : null;

				if ( id == null && machineType == null && name == null && zone == null)
				{
					return null;
				}

				return new GcpVendorModel(id, machineType, name, zone);
			}
			catch
			{
				return null;
			}
		}

		[CanBeNull]
		public IVendorModel GetPcfVendorInfo()
		{
			var instanceGuid = NormalizeAndValidateMetadata(GetProcessEnvironmentVariable(PcfInstanceGuid), "cf_instance_guid", PcfName);
			var instanceIp = NormalizeAndValidateMetadata(GetProcessEnvironmentVariable(PcfInstanceIp), "cf_instance_ip", PcfName);
			var memoryLimit = NormalizeAndValidateMetadata(GetProcessEnvironmentVariable(PcfMemoryLimit), "memory_limit", PcfName);

			if ( instanceGuid == null && instanceIp == null && memoryLimit == null)
			{
				return null;
			}
			else
			{
				return new PcfVendorModel(instanceGuid, instanceIp, memoryLimit);
			}
		}

		[CanBeNull]
		private string GetProcessEnvironmentVariable([NotNull]string variableName)
		{
			try
			{
				var variableValue = System.Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Process);
				return variableValue;
			}
			catch
			{
				return null;
			}
		}

		[CanBeNull]
		private IVendorModel GetDockerVendorInfo()
		{
#if NETSTANDARD2_0
			bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
			int subsystemsIndex = 1;
			int controlGroupIndex = 2;

			if (isLinux)
			{
				try
				{
					string id = null;
					var fileLines = File.ReadAllLines("/proc/self/cgroup");

					foreach(var line in fileLines)
					{
						var elements = line.Split(':');
						var cpuSubsystem = elements[subsystemsIndex].Split(',').FirstOrDefault(subsystem => subsystem == "cpu");
						if (cpuSubsystem != null)
						{
							var controlGroup = elements[controlGroupIndex];
							var match = Regex.Match(controlGroup, ContainerIdRegex);
							
							if (match.Success)
							{
								id = match.Value;
							}
						}
					}

					if(id == null)
					{
						return null;
					}

					return new DockerVendorModel(id);
				}
				catch
				{
					return null;
				}
				
			}
#endif
			return null;
		}

		[CanBeNull]
		private string GetHttpResponseString([NotNull] Uri uri, [CanBeNull]IEnumerable<string> headers = null)
		{
			try
			{
				var request = WebRequest.Create(uri);
				request.Method = "GET";
				request.Timeout = WebReqeustTimeout;

				if (headers != null)
				{
					foreach (var header in headers)
					{
						request.Headers.Add(header);
					}
				}

				using (var response = request.GetResponse() as HttpWebResponse)
				{
					if (response == null)
						return null;

					var stream = response.GetResponseStream();
					if (stream == null)
						return null;

					var reader = new StreamReader(stream);

					return reader.ReadToEnd();
				}
			}
			catch
			{
				return null;
			}
		}

		[CanBeNull]
		public string NormalizeAndValidateMetadata([CanBeNull]string metadataValue, [NotNull]string metadataField, [NotNull]string vendorName)
		{
			if (metadataValue == null)
				return null;

			var normalizedValue = NormalizeString(metadataValue);

			if (normalizedValue.IsEmpty())
				return null;

			if (!IsValidMetadata(normalizedValue))
			{
				Log.InfoFormat("Unable to validate {0} metadata for the {1} field.", vendorName, metadataField);

				switch (vendorName)
				{
					case AwsName:
						_agentHealthReporter.ReportAwsUtilizationError();
						break;
					case AzureName:
						_agentHealthReporter.ReportAzureUtilizationError();
						break;
					case GcpName:
						_agentHealthReporter.ReportGcpUtilizationError();
						break;
					case PcfName:
						_agentHealthReporter.ReportPcfUtilizationError();
						break;
					case DockerName:
						_agentHealthReporter.ReportBootIdError();
						break;
				}

				return null;
			}

			return normalizedValue;
		}

		[NotNull]
		private string NormalizeString([NotNull] string data)
		{
			return Clamper.ClampLength(data.Trim(), 255);
		}

		[NotNull]
		public bool IsValidMetadata(string data)
		{
			return Regex.IsMatch(data, ValidateMetadataRegex);
		}
	}
}
