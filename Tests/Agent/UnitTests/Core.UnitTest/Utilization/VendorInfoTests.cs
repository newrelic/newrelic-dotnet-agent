using System;
using System.Linq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Configuration;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;


namespace NewRelic.Agent.Core.Utilization
{

	[TestFixture]
	public class VendorInfoTests
	{
		private IConfiguration _configuration;
		private IAgentHealthReporter _agentHealthReporter;
		private IEnvironment _environment;
		private VendorHttpApiRequestor _vendorHttpApiRequestor;

		private const string PcfInstanceGuid = @"CF_INSTANCE_GUID";
		private const string PcfInstanceIp = @"CF_INSTANCE_IP";
		private const string PcfMemoryLimit = @"MEMORY_LIMIT";

		[SetUp]
		public void Setup()
		{
			_configuration = Mock.Create<IConfiguration>();
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>();
			_environment = Mock.Create<IEnvironment>();
			_vendorHttpApiRequestor = Mock.Create<VendorHttpApiRequestor>();
		}

		[Test]
		public void GetVendors_Returns_Empty_Dictionary_When_Detect_True_And_Data_Unavailable()
		{
			Mock.Arrange(() => _configuration.UtilizationDetectAws).Returns(true);
			Mock.Arrange(() => _configuration.UtilizationDetectAzure).Returns(true);
			Mock.Arrange(() => _configuration.UtilizationDetectGcp).Returns(true);
			Mock.Arrange(() => _configuration.UtilizationDetectPcf).Returns(true);
			Mock.Arrange(() => _configuration.UtilizationDetectDocker).Returns(true);

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var vendors = vendorInfo.GetVendors();
			Assert.IsFalse(vendors.Any());
		}

		[Test]
		public void GetVendors_Returns_Empty_Dictionary_When_Detect_False()
		{
			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var vendors = vendorInfo.GetVendors();
			Assert.IsFalse(vendors.Any());
		}

		[TestCase("?", "location", "azure", null)]
		[TestCase("westus2", "location", "azure", "westus2")]
		[TestCase("<script>do something </script>", "zone", "gcp", null )]
		[TestCase("ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd260", "zone", "aws", "ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd")]
		public void GetVendors_NormalizeAndValidateMetadata(string metadataValue, string metadataField, string vendorName, string expectedResponse)
		{
			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var result = vendorInfo.NormalizeAndValidateMetadata(metadataValue, metadataField, vendorName);

			if (expectedResponse == null)
			{
				Assert.Null(result);
			}
			else
			{
				Assert.True(result.Equals(expectedResponse));
			}
		}

		[Test]
		public void GetVendors_ParseAwsVendorInfo_Complete()
		{
			var json = @"{
							""availabilityZone"" : ""us - east - 1d"",
							""instanceId"" : ""i-1234567890abcdef0"",
							""instanceType"" : ""t1.micro""
						}";

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (AwsVendorModel)vendorInfo.ParseAwsVendorInfo(json);

			Assert.NotNull(model);
			Assert.True(model.InstanceId == "i-1234567890abcdef0");
			Assert.True(model.InstanceType == "t1.micro");
			Assert.True(model.AvailabilityZone == "us - east - 1d");
		}

		[Test]
		public void GetVendors_ParseAwsVendorInfo_MissingInvalidValues()
		{
			var json = @"{
							""instanceId"" : ""i-1234567890abcdef0"",
							""instanceType"" : ""t1.$micro""
						}";

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (AwsVendorModel)vendorInfo.ParseAwsVendorInfo(json);

			Assert.NotNull(model);
			Assert.True(model.InstanceId == "i-1234567890abcdef0");
			Assert.Null(model.InstanceType);
			Assert.Null(model.AvailabilityZone);
		}

		[Test]
		public void GetVendors_ParseAwsVendorInfo_InvalidJson()
		{
			var json = @"{  I am not valid json. Deal with it.
							""availabilityZone"" : ""us - east - 1d"",
							""instanceId"" : ""i-1234567890abcdef0"",
							""instanceType"" : ""t1.micro""
						}";

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = vendorInfo.ParseAwsVendorInfo(json);

			Assert.Null(model);
		}

		[Test]
		public void GetVendors_ParseAzureVendorInfo_Complete()
		{
			var json = @"{
							  ""location"": ""CentralUS"",
							  ""name"": ""IMDSCanary"",
							  ""vmId"": ""5c08b38e-4d57-4c23-ac45-aca61037f084"",
							  ""vmSize"": ""Standard_DS2""
						}";

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (AzureVendorModel)vendorInfo.ParseAzureVendorInfo(json);

			Assert.NotNull(model);
			Assert.True(model.Location == "CentralUS");
			Assert.True(model.Name == "IMDSCanary");
			Assert.True(model.VmId == "5c08b38e-4d57-4c23-ac45-aca61037f084");
			Assert.True(model.VmSize == "Standard_DS2");
		}

		[Test]
		public void GetVendors_ParseAzureVendorInfo_MissingInvalidValues()
		{
			var json = @"{
							  ""location"": ""CentralUS"",
							  ""name"": ""IMDS:Canary"",
							  ""vmId"": ""5c08b38e-4d57-4c23-ac45-aca61037f084""
						}";

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (AzureVendorModel)vendorInfo.ParseAzureVendorInfo(json);

			Assert.NotNull(model);
			Assert.True(model.Location == "CentralUS");
			Assert.Null(model.Name);
			Assert.True(model.VmId == "5c08b38e-4d57-4c23-ac45-aca61037f084");
			Assert.Null(model.VmSize);
		}

		[Test]
		public void GetVendors_ParseAzureVendorInfo_InvalidJson()
		{
			var json = @"{
							  I am not valid json. Deal with it.
							  ""location"": ""CentralUS"",
							  ""name"": ""IMDSCanary"",
							  ""vmId"": ""5c08b38e-4d57-4c23-ac45-aca61037f084"",
							  ""vmSize"": ""Standard_DS2""
						}";

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (AzureVendorModel)vendorInfo.ParseAzureVendorInfo(json);

			Assert.Null(model);
		}

		[Test]
		public void GetVendors_ParseGcpVendorInfo_Complete()
		{
			var json = @"{
							""id"": 3161347020215157000,
							""machineType"": ""projects / 492690098729 / machineTypes / custom - 1 - 1024"",
							""name"": ""aef-default-20170501t160547-7gh8"",
							""zone"": ""projects/492690098729/zones/us-central1-c""
						}";

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (GcpVendorModel)vendorInfo.ParseGcpVendorInfo(json);

			Assert.NotNull(model);
			Assert.True(model.Id == "3161347020215157000");
			Assert.True(model.MachineType == "custom - 1 - 1024");
			Assert.True(model.Name == "aef-default-20170501t160547-7gh8");
			Assert.True(model.Zone == "us-central1-c");
		}

		[Test]
		public void GetVendors_ParseGcpVendorInfo_MissingInvalidValues()
		{
			var json = @"{
							""id"": 3161347020215157000,
							""machineType"": ""projects / 492690098729 / machineTypes / custom - 1 - 1024"",
							""name"": ""aef-default-20170501t160547-7gh8?""
						}";

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (GcpVendorModel)vendorInfo.ParseGcpVendorInfo(json);

			Assert.NotNull(model);
			Assert.True(model.Id == "3161347020215157000");
			Assert.True(model.MachineType == "custom - 1 - 1024");
			Assert.Null(model.Name);
			Assert.Null(model.Zone);
		}

		[Test]
		public void GetVendors_ParseGcpVendorInfo_InvalidJson()
		{
			var json = @"{
							""id"": 3161347020215157000,
							""machineType"": ""projects / 492690098729 / machineTypes / custom - 1 - 1024"",
							""name"": ""aef-default-20170501t160547-7gh8""
							I'm not valid json. Deal with it.
						}";

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (GcpVendorModel)vendorInfo.ParseGcpVendorInfo(json);

			Assert.Null(model);
		}

		[Test]
		public void GetVendors_GetPcfVendorInfo_Complete()
		{
			SetEnvironmentVariable(PcfInstanceGuid, "b977d090-83db-4bdb-793a-bb77", EnvironmentVariableTarget.Process);
			SetEnvironmentVariable(PcfInstanceIp, "10.10.147.130", EnvironmentVariableTarget.Process);
			SetEnvironmentVariable(PcfMemoryLimit, "1024m", EnvironmentVariableTarget.Process);

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (PcfVendorModel)vendorInfo.GetPcfVendorInfo();

			Assert.NotNull(model);
			Assert.True(model.CfInstanceGuid == "b977d090-83db-4bdb-793a-bb77");
			Assert.True(model.CfInstanceIp == "10.10.147.130");
			Assert.True(model.MemoryLimit == "1024m");
		}

		[Test]
		public void GetVendors_GetPcfVendorInfo_None()
		{
			SetEnvironmentVariable(PcfInstanceGuid, null, EnvironmentVariableTarget.Process);
			SetEnvironmentVariable(PcfInstanceIp, null, EnvironmentVariableTarget.Process);
			SetEnvironmentVariable(PcfMemoryLimit, null, EnvironmentVariableTarget.Process);

			var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
			var model = (PcfVendorModel)vendorInfo.GetPcfVendorInfo();

			Assert.Null(model);
		}

		private void SetEnvironmentVariable(string variableName, string value, EnvironmentVariableTarget environmentVariableTarget)
		{
			Mock.Arrange(() => _environment.GetEnvironmentVariable(variableName, environmentVariableTarget)).Returns(value);
		}
	}
}
